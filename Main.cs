﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Resources;
using System.Net;
using AngleSharp.Parser.Html;
using AngleSharp.Dom;
using System.Reflection;
using WindowsInput;
using WindowsInput.Native;

namespace ClipAngel
{
    enum PasteMethod {Standart, PasteText, SendChars };
    public partial class Main : Form
    {
        public ResourceManager CurrentLangResourceManager;
        public string Locale = "";
        public bool PortableMode = false;
        public int ClipsNumber = 0;
        public string UserSettingsPath;
        public string DbFileName;
        public bool StartMinimized = false;
        SQLiteConnection m_dbConnection;
        public string ConnectionString;
        SQLiteDataAdapter dataAdapter;
        //bool CaptureClipboard = true;
        bool allowRowLoad = true;
        //bool AutoGotoLastRow = true;
        bool AllowFormClose = false;
        bool AllowHotkeyProcess = true;
        bool EditMode = false;
        SQLiteDataReader RowReader;
        static string LinkPattern = "\\b(https?|ftp|file)://[-A-Z0-9+&@#/%?=~_|!:,.;]*[A-Z0-9+&@#/%=~_|]";
        int LastId = 0;
        MatchCollection TextLinkMatches;
        MatchCollection UrlLinkMatches;
        MatchCollection FilterMatches;
        string DataFormat_ClipboardViewerIgnore = "Clipboard Viewer Ignore";
        string ActualVersion;
        //DateTime lastAutorunUpdateCheck;
        int MaxTextViewSize = 5000;
        bool TextWasCut;
        KeyboardHook hook = new KeyboardHook();
        WinEventDelegate dele = null;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private IntPtr lastActiveWindow;
        private IntPtr HookChangeActiveWindow;
        private bool AllowFilterProcessing = true;
        private static Color favouriteColor = Color.FromArgb(255, 220, 220);
        private static Color _usedColor = Color.FromArgb(200, 255, 255);
        Bitmap imageText;
        Bitmap imageHtml;
        Bitmap imageRtf;
        Bitmap imageFile;
        Bitmap imageImg;
        string filterText = ""; // To optimize speed
        readonly RichTextBox _richTextBox = new RichTextBox();

        public Main()
        {
            UpdateCurrentCulture(); // Antibug. Before bug it was not required
            InitializeComponent();
            dele = new WinEventDelegate(WinEventProc);
            HookChangeActiveWindow = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
            // register the event that is fired after the key press.
            hook.KeyPressed +=
                new EventHandler<KeyPressedEventArgs>(hook_KeyPressed);
            RegisterHotKeys();
            timerCheckUpdate.Interval = (1000 * 60 * 60 * 24); // 1 day
            timerCheckUpdate.Start();
            timerReconnect.Interval = (1000 * 5 ); // 5 seconds
            timerReconnect.Start();
        }
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (GetTopParentWindow(hwnd) != this.Handle)
                lastActiveWindow = hwnd;
        }

        public static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
        private void RegisterHotKeys()
        {
            EnumModifierKeys Modifiers;
            Keys Key;
            if (ReadHotkeyFromText(Properties.Settings.Default.HotkeyShow, out Modifiers, out Key))
                hook.RegisterHotKey(Modifiers, Key);
            if (ReadHotkeyFromText(Properties.Settings.Default.HotkeyIncrementalPaste, out Modifiers, out Key))
                hook.RegisterHotKey(Modifiers, Key);
        }

        private static bool ReadHotkeyFromText(string HotkeyText, out EnumModifierKeys Modifiers, out Keys Key)
        {
            Modifiers = 0;
            Key = 0;
            if (HotkeyText == "" || HotkeyText == "No")
                return false;
            string[] Frags = HotkeyText.Split(new[] { " + " }, StringSplitOptions.None);
            for (int i = 0; i < Frags.Length - 1; i++)
            {
                EnumModifierKeys Modifier = 0;
                Enum.TryParse(Frags[i], out Modifier);
                Modifiers |= Modifier;
            }
            Enum.TryParse(Frags[Frags.Length - 1], out Key);
            return true;
        }

        void hook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!AllowHotkeyProcess)
                return;
            string hotkeyTitle = KeyboardHook.HotkeyTitle(e.Key, e.Modifier);
            if (hotkeyTitle == Properties.Settings.Default.HotkeyShow)
            {
                ShowForPaste();
                dataGridView.Focus();
            }
            else if (hotkeyTitle == Properties.Settings.Default.HotkeyIncrementalPaste)
            {
                AllowHotkeyProcess = false;
                SendPaste();
                if ((e.Modifier & EnumModifierKeys.Alt) != 0)
                    keybd_event((byte)VirtualKeyCode.MENU, 0x38, 0, 0); // LEFT
                if ((e.Modifier & EnumModifierKeys.Control) != 0)
                    keybd_event((byte)VirtualKeyCode.CONTROL, 0x1D, 0, 0);
                if ((e.Modifier & EnumModifierKeys.Shift) != 0)
                    keybd_event((byte)VirtualKeyCode.SHIFT, 0x2A, 0, 0);
                clipBindingSource.MoveNext();
                DataRow CurrentDataRow = ((DataRowView)clipBindingSource.Current).Row;
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(2000, CurrentLangResourceManager.GetString("NextClip"), CurrentDataRow["Title"] as string, ToolTipIcon.Info);
                AllowHotkeyProcess = true;
            }
            else
            {
                //int a = 0;
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch ((Msgs)m.Msg)
            {
                case Msgs.WM_CLIPBOARDUPDATE:
                    Debug.WriteLine("WindowProc WM_CLIPBOARDUPDATE: " + m.Msg, "WndProc");
                    GetClipboardData();
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }

        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private void Main_Load(object sender, EventArgs e)
        {
            ResourceManager resourceManager = Properties.Resources.ResourceManager;
            imageText = resourceManager.GetObject("TypeText") as Bitmap;
            imageHtml = resourceManager.GetObject("TypeHtml") as Bitmap;
            imageRtf = resourceManager.GetObject("TypeRtf") as Bitmap;
            imageFile = resourceManager.GetObject("TypeFile") as Bitmap;
            imageImg = resourceManager.GetObject("TypeImg") as Bitmap;
            TypeFilter.SelectedIndex = 0;
            MarkFilter.SelectedIndex = 0;
            richTextBox.AutoWordSelection = false;
            textBoxUrl.AutoWordSelection = false;
            CheckUpdate();
            LoadSettings();
            if (Properties.Settings.Default.LastFilterValues == null)
            {
                Properties.Settings.Default.LastFilterValues = new StringCollection();
            }
            FillFilterItems();
            if (!Directory.Exists(UserSettingsPath))
            {
                Directory.CreateDirectory(UserSettingsPath);
            }
            DbFileName = UserSettingsPath + "\\" + Properties.Resources.DBShortFilename;
            ConnectionString = "data source=" + DbFileName + ";";
            string Reputation = "Magic67234784";
            if (!File.Exists(DbFileName))
            {
                File.WriteAllBytes(DbFileName, Properties.Resources.dbTemplate);
                m_dbConnection = new SQLiteConnection(ConnectionString);
                m_dbConnection.Open();
                // Encryption http://stackoverflow.com/questions/12190672/can-i-password-encrypt-sqlite-database
                m_dbConnection.ChangePassword(Reputation);
                m_dbConnection.Close();
            }
            ConnectionString += "Password = " + Reputation + ";";
            m_dbConnection = new SQLiteConnection(ConnectionString);
            m_dbConnection.Open();
            SQLiteCommand command;

            command = new SQLiteCommand("ALTER TABLE Clips" + " ADD COLUMN Hash CHAR(32)", m_dbConnection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch
            {
            }
            command = new SQLiteCommand("ALTER TABLE Clips" + " ADD COLUMN Favorite BOOLEAN", m_dbConnection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch
            {
            }
            command = new SQLiteCommand("ALTER TABLE Clips" + " ADD COLUMN ImageSample BINARY", m_dbConnection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch
            {
            }
            command = new SQLiteCommand("CREATE unique index unique_hash on Clips(hash)", m_dbConnection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            // http://blog.tigrangasparian.com/2012/02/09/getting-started-with-sqlite-in-c-part-one/
            //sql = "CREATE TABLE Clips (Title VARCHAR(50), Text VARCHAR(0), Data BLOB, Size INT, Type VARCHAR(10), Created DATETIME, Application VARCHAR(50), Window VARCHAR(100))";
            //command = new SQLiteCommand(sql, m_dbConnection);
            //try
            //{
            //    command.ExecuteNonQuery();
            //}
            //catch { };

            // https://msdn.microsoft.com/ru-ru/library/fbk67b6z(v=vs.110).aspx
            dataAdapter = new SQLiteDataAdapter("", ConnectionString);
            //dataGridView.DataSource = clipBindingSource;
            UpdateClipBindingSource();
            ConnectClipboard();
            if (StartMinimized)
            {
                StartMinimized = false;
                this.ShowInTaskbar = false;
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void ConnectClipboard()
        {
            if (!AddClipboardFormatListener(this.Handle))
            {
                int ErrorCode = Marshal.GetLastWin32Error();
                int ERROR_INVALID_PARAMETER = 87;
                if (ErrorCode!= ERROR_INVALID_PARAMETER)
                    Debug.WriteLine("Failed to connect clipboard: " + Marshal.GetLastWin32Error());
                else
                {
                    //already connected
                }
            }
        }

        private void AfterRowLoad(int CurrentRowIndex = -1, bool FullTextLoad = false, int selectionStart = 0, int selectionLength = 0)
        {
            DataRowView CurrentRowView;
            string ClipType;
            FullTextLoad = FullTextLoad || EditMode;
            richTextBox.ReadOnly = !EditMode;
            if (CurrentRowIndex == -1)
            { CurrentRowView = clipBindingSource.Current as DataRowView; }
            else
            { CurrentRowView = clipBindingSource[CurrentRowIndex] as DataRowView; }
            FilterMatches = null;
            if (CurrentRowView == null)
            {
                ClipType = "";
                richTextBox.Text = "";
                textBoxApplication.Text = "";
                textBoxWindow.Text = "";
                StripLabelCreated.Text = "";
                StripLabelSize.Text = "";
                StripLabelVisualSize.Text = "";
                StripLabelType.Text = "";
                StripLabelPosition.Text = "";
            }
            else
            {
                DataRow CurrentRow = CurrentRowView.Row;
                string sql = "SELECT * FROM Clips Where Id = @Id";
                SQLiteCommand commandSelect = new SQLiteCommand(sql, m_dbConnection);
                commandSelect.Parameters.Add("@Id", DbType.Int32).Value = CurrentRow["Id"];
                RowReader = commandSelect.ExecuteReader();
                RowReader.Read();
                ClipType = RowReader["type"].ToString();
                textBoxApplication.Text = RowReader["Application"].ToString();
                textBoxWindow.Text = RowReader["Window"].ToString();
                StripLabelCreated.Text = RowReader["Created"].ToString();
                StripLabelSize.Text = RowReader["Size"] + MultiLangByteUnit();
                StripLabelVisualSize.Text = RowReader["Chars"]+ MultiLangCharUnit();
                string TypeEng = RowReader["Type"].ToString();
                if (CurrentLangResourceManager.GetString(TypeEng) == null)
                    StripLabelType.Text = TypeEng;
                else
                    StripLabelType.Text = CurrentLangResourceManager.GetString(TypeEng);
                StripLabelPosition.Text = "1";

                // to prevent autoscrolling during marking
                richTextBox.HideSelection = true;
                richTextBox.Clear();
                string fullText = RowReader["Text"].ToString();
                string shortText;
                string endMarker;
                Font markerFont;
                Color markerColor;
                if (!FullTextLoad && MaxTextViewSize < fullText.Length)
                {
                    shortText = fullText.Substring(1, MaxTextViewSize);
                    endMarker = MultiLangCutMarker();
                    markerFont = new Font(richTextBox.SelectionFont, FontStyle.Underline);
                    TextWasCut = true;
                    markerColor = Color.Blue;
                }
                else
                {
                    shortText = fullText;
                    endMarker = MultiLangEndMarker();
                    markerFont = richTextBox.SelectionFont;
                    TextWasCut = false;
                    markerColor = Color.Green;
                }
                richTextBox.Text = shortText;
                if (!EditMode)
                {
                    richTextBox.SelectionStart = richTextBox.TextLength;
                    richTextBox.SelectionColor = markerColor;
                    richTextBox.SelectionFont = markerFont;
                    richTextBox.AppendText(endMarker); // Do it first, else ending hyperlink will connect underline to it
                    MarkLinksInRichTextBox(richTextBox, out TextLinkMatches);
                    if (filterText.Length > 0)
                    {
                        MarkRegExpMatchesInRichTextBox(richTextBox, Regex.Escape(filterText).Replace("%", ".*?"), Color.Red, false, out FilterMatches);
                    }
                }
                richTextBox.SelectionColor = new Color();
                richTextBox.SelectionStart = 0;
                //richTextBox.HideSelection = false; // slow

                textBoxUrl.HideSelection = true;
                textBoxUrl.Clear();
                textBoxUrl.Text = RowReader["Url"].ToString();
                MarkLinksInRichTextBox(textBoxUrl, out UrlLinkMatches);

                if (ClipType == "img")
                {
                    Image image = GetImageFromBinary((byte[])RowReader["Binary"]);
                    ImageControl.Image = image;
                }
            }
            if (ClipType == "img")
            {
                tableLayoutPanelData.RowStyles[0].Height = 20;
                tableLayoutPanelData.RowStyles[1].Height = 80;
            }
            else
            {
                tableLayoutPanelData.RowStyles[0].Height = 100;
                tableLayoutPanelData.RowStyles[1].Height = 0;
            }
            if (textBoxUrl.Text == "")
                tableLayoutPanelData.RowStyles[2].Height = 0;
            else
                tableLayoutPanelData.RowStyles[2].Height = 25;
            if (EditMode)
                richTextBox.Focus();
            if (selectionStart != 0)
            {
                richTextBox.SelectionStart = selectionStart;
                richTextBox.SelectionLength = selectionLength;
                richTextBox.HideSelection = false;
            }
        }

        private string FormatByteSize(int byteSize)
        {
            string[] sizes = { MultiLangByteUnit(), MultiLangKiloByteUnit(), MultiLangMegaByteUnit() };
            double len = byteSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            string result = String.Format("{0:0.#} {1}", len, sizes[order]);
            return result;
        }

        private string MultiLangEndMarker()
        {
            return "<" + CurrentLangResourceManager.GetString("EndMarker") + ">";
        }

        private string MultiLangCutMarker()
        {
            return "<" + CurrentLangResourceManager.GetString("CutMarker") + ">";
        }

        private string MultiLangCharUnit()
        {
            return CurrentLangResourceManager.GetString("CharUnit");
        }

        private string MultiLangByteUnit()
        {
            return CurrentLangResourceManager.GetString("ByteUnit");
        }
        private string MultiLangKiloByteUnit()
        {
            return CurrentLangResourceManager.GetString("KiloByteUnit");
        }
        private string MultiLangMegaByteUnit()
        {
            return CurrentLangResourceManager.GetString("MegaByteUnit");
        }

        private void MarkLinksInRichTextBox(RichTextBox control, out MatchCollection matches)
        {
            MarkRegExpMatchesInRichTextBox(control, LinkPattern, Color.Blue, true, out matches);
        }

        private void MarkRegExpMatchesInRichTextBox(RichTextBox control, string pattern, Color color, bool underline, out MatchCollection matches)
        {
            matches = Regex.Matches(control.Text, pattern, RegexOptions.IgnoreCase);
            control.DeselectAll();
            int maxMarked = 100; // prevent slow down
            foreach (Match match in matches)
            {
                control.SelectionStart = match.Index;
                control.SelectionLength = match.Length;
                control.SelectionColor = color;
                if (underline)
                    control.SelectionFont = new Font(control.SelectionFont, FontStyle.Underline);
                maxMarked--;
                if (maxMarked < 0)
                    break;
            }
            control.DeselectAll();
            control.SelectionColor = new Color();
            control.SelectionFont = new Font(control.SelectionFont, FontStyle.Regular);
        }

        private void RichText_Click(object sender, EventArgs e)
        {
            string newText = "" + richTextBox.SelectionStart;
            if (richTextBox.SelectionLength > 0)
            {
                //NewText += "+" + Text.SelectionLength + "=" + (Text.SelectionStart + Text.SelectionLength);
                newText += "+" + richTextBox.SelectionLength;
            }
            StripLabelPosition.Text = newText;

            //NewText = "" + Text.Cursor;
            //StripLabelPositionXY.Text = NewText;
            OpenLinkIfCtrlPressed(sender as RichTextBox, e, TextLinkMatches);
            if (MaxTextViewSize >= (sender as RichTextBox).SelectionStart && TextWasCut)
                AfterRowLoad(-1, true, richTextBox.SelectionStart, richTextBox.SelectionLength);
        }

        private void Filter_TextChanged(object sender, EventArgs e)
        {
            if (AllowFilterProcessing)
                timerApplyTextFiler.Start();
        }

        private void TextFilterApply()
        {
            ReadFilterText();
            ChooseTitleColumnDraw();
            UpdateClipBindingSource(true);
        }

        private void UpdateClipBindingSource(bool forceRowLoad = false, int currentClipId = 0)
        {
            if (dataAdapter == null)
                return;
            if (EditMode)
                SaveClipText();
            if (currentClipId == 0 && clipBindingSource.Current != null)
                currentClipId = (int)(clipBindingSource.Current as DataRowView).Row["Id"];
            allowRowLoad = false;
            string sqlFilter = "1 = 1";
            string filterValue = "";
            bool filterOn = false;
            if (filterText != "")
            {
                sqlFilter += " AND UPPER(Text) Like UPPER('%" + filterText + "%')";
                filterOn = true;
            }
            if (TypeFilter.SelectedValue as string != "allTypes")
            {
                filterValue = TypeFilter.SelectedValue as string;
                if (filterValue == "text")
                    filterValue = "'html','rtf','text'";
                else
                    filterValue = "'" + filterValue + "'";
                sqlFilter += " AND type IN (" + filterValue + ")";
                filterOn = true;
            }
            if (MarkFilter.SelectedValue as string != "allMarks")
            {
                filterValue = MarkFilter.SelectedValue as string;
                sqlFilter += " AND " + filterValue;
                filterOn = true;
            }
            string selectCommandText = "Select Id, Used, Title, Chars, Type, Favorite, ImageSample From Clips";
            selectCommandText += " WHERE " + sqlFilter;
            selectCommandText += " ORDER BY Id desc";
            dataAdapter.SelectCommand.CommandText = selectCommandText;

            DataTable table = new DataTable();
            table.Locale = CultureInfo.InvariantCulture;
            dataAdapter.Fill(table);
            clipBindingSource.DataSource = table;

            PrepareTableGrid(); // Long
            if (filterOn)
                buttonClearFilter.BackColor = Color.GreenYellow;
            else
                buttonClearFilter.BackColor = DefaultBackColor;
            if (LastId == 0)
            {
                GotoLastRow();
                ClipsNumber = clipBindingSource.Count;
                DataRowView lastRow = (DataRowView)clipBindingSource.Current;
                if (lastRow == null)
                {
                    LastId = 0;
                }
                else
                {
                    LastId = (int)lastRow["Id"];
                }
            }
            else if (false
                //|| AutoGotoLastRow 
                || currentClipId <= 0)
                GotoLastRow();
            else if (currentClipId > 0)
            {
                clipBindingSource.Position = clipBindingSource.Find("Id", currentClipId);
                ////if (dataGridView.CurrentRow != null)
                ////    dataGridView.CurrentCell = dataGridView.CurrentRow.Cells[0];
                SelectCurrentRow(forceRowLoad);
            }
            allowRowLoad = true;
            //AutoGotoLastRow = false;
        }

        private void ClearFilter_Click(object sender = null, EventArgs e = null)
        {
            ClearFilter();
        }

        private void ClearFilter(int CurrentClipID = 0)
        {
            AllowFilterProcessing = false;
            comboBoxFilter.Text = "";
            ReadFilterText();
            TypeFilter.SelectedIndex = 0;
            MarkFilter.SelectedIndex = 0;
            dataGridView.Focus();
            AllowFilterProcessing = true;
            ChooseTitleColumnDraw();
            UpdateClipBindingSource(true, CurrentClipID);
        }

        private void Text_CursorChanged(object sender, EventArgs e)
        {
            // This event not working. Why? Decided to use Click instead.
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AllowFormClose)
            {
                Hide();
                e.Cancel = true;
                if (Properties.Settings.Default.ClearFiltersOnClose)
                    ClearFilter();
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetClipboardOwner();

        private void GetClipboardData()
        {
            //
            // Data on the clipboard uses the 
            // IDataObject interface
            //
            //if (!CaptureClipboard)
            //{ return; }
            IDataObject iData = new DataObject();
            string clipType = "";
            string clipText = "";
            string clipWindow = "";
            string clipApplication = "";
            string richText = "";
            string htmlText = "";
            string clipUrl = "";
            int clipChars = 0;
            GetClipboardOwnerInfo(out clipWindow, out clipApplication);
            try
            {
                iData = Clipboard.GetDataObject();
            }
            catch (ExternalException externEx)
            {
                // Copying a field definition in Access 2002 causes this sometimes?
                Debug.WriteLine("Clipboard.GetDataObject(): InteropServices.ExternalException: {0}", externEx.Message);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), Application.ProductName);
                return;
            }
            if (iData.GetDataPresent(DataFormat_ClipboardViewerIgnore))
                return;
            if (iData.GetDataPresent(DataFormats.UnicodeText))
            {
                clipText = (string)iData.GetData(DataFormats.UnicodeText);
                clipType = "text";
                //Debug.WriteLine(Text);
            }
            else if (iData.GetDataPresent(DataFormats.Text))
            {
                clipText = (string)iData.GetData(DataFormats.Text);
                clipType = "text";
                //Debug.WriteLine(Text);
            }

            if (iData.GetDataPresent(DataFormats.Rtf))
            {
                richText = (string)iData.GetData(DataFormats.Rtf);
                if (iData.GetDataPresent(DataFormats.Text))
                {
                    clipType = "rtf";
                }
            }
            if (iData.GetDataPresent(DataFormats.Html))
            {
                htmlText = (string)iData.GetData(DataFormats.Html);
                if (iData.GetDataPresent(DataFormats.Text))
                {
                    clipType = "html";
                    Match match = Regex.Match(htmlText, "SourceURL:(" + LinkPattern + ")", RegexOptions.IgnoreCase);
                    if (match.Captures.Count > 0)
                        clipUrl = match.Groups[1].ToString();
                }
            }

            //StringCollection UrlFormatNames = new StringCollection();
            //UrlFormatNames.Add("text/x-moz-url-priv");
            //UrlFormatNames.Add("msSourceUrl");
            //foreach (string UrlFormatName in UrlFormatNames)
            //    if (iData.GetDataPresent(UrlFormatName))
            //    {
            //        var ms = (MemoryStream)iData.GetData(UrlFormatName);
            //        var sr = new StreamReader(ms, Encoding.Unicode, true);
            //        Url = sr.ReadToEnd();
            //        break;
            //    }

            if (iData.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileNameList = iData.GetData(DataFormats.FileDrop) as string[];
                if (fileNameList != null)
                {
                    clipText = String.Join("\n", fileNameList);
                    if (iData.GetDataPresent(DataFormats.FileDrop))
                    {
                        clipType = "file";
                    }
                }
                else
                {
                    // Coping Outlook task
                }
            }

            byte[] binaryBuffer = new byte[0];
            byte[] imageSampleBuffer = new byte[0];
            // http://www.cyberforum.ru/ado-net/thread832314.html
            if (iData.GetDataPresent(DataFormats.Bitmap))
            {
                clipType = "img";
                Bitmap image = iData.GetData(DataFormats.Bitmap) as Bitmap;
                if (image == null)
                    // Happans while copying image in standart image viewer Windows 10
                    return;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    binaryBuffer = memoryStream.ToArray();
                }
                if (clipText == "")
                {
                    clipText = CurrentLangResourceManager.GetString("Size") + ": " + image.Width + "x" + image.Height + "\n"
                         + CurrentLangResourceManager.GetString("PixelFormat") + ": " + image.PixelFormat + "\n";
                }
                clipChars = image.Width * image.Height;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    Image ImageSample = CopyRectImage(image, new Rectangle(0, 0, 100, 20));
                    ImageSample.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    imageSampleBuffer = memoryStream.ToArray();
                }
                // OCR
                //try
                //{
                //    TesseractEngine ocr = new TesseractEngine("./tessdata", "eng", EngineMode.TesseractAndCube);
                //    string fileName = Path.GetTempFileName();
                //    image.Save(fileName);
                //    Pix pix = Pix.LoadFromFile(fileName);
                //    var result = ocr.Process(pix, PageSegMode.Auto);
                //    clipText = result.GetText();
                //}
                //catch (Exception e)
                //{
                //}
            }

            if (clipType != "")
            {
                AddClip(binaryBuffer, imageSampleBuffer, htmlText, richText, clipType, clipText, clipApplication, clipWindow, clipUrl, clipChars);
                //UpdateClipBindingSource();
            }

        }
        void AddClip(byte[] binaryBuffer = null, byte[] imageSampleBuffer = null, string htmlText = "", string richText = "", string typeText = "text", string plainText = "", string applicationText = "", string windowText = "", string url = "", int chars = 0)
        {
            if (plainText == null)
                plainText = "";
            if (richText == null)
                richText = "";
            if (htmlText == null)
                htmlText = "";
            int byteSize = plainText.Length * 2; // dirty
            if (chars == 0)
                chars = plainText.Length;
            LastId = LastId + 1;
            if (binaryBuffer != null)
                byteSize += binaryBuffer.Length;
            byteSize += htmlText.Length * 2; // dirty
            byteSize += richText.Length * 2; // dirty
            if (byteSize > Properties.Settings.Default.MaxClipSizeKB * 1000)
                return;
            DateTime created = DateTime.Now;
            string clipTitle = TextClipTitle(plainText);
            MD5 md5 = new MD5CryptoServiceProvider();
            if (binaryBuffer != null)
                md5.TransformBlock(binaryBuffer, 0, binaryBuffer.Length, binaryBuffer, 0);
            byte[] binaryText = Encoding.Unicode.GetBytes(plainText);
            md5.TransformBlock(binaryText, 0, binaryText.Length, binaryText, 0);
            byte[] binaryRichText = Encoding.Unicode.GetBytes(richText);
            md5.TransformBlock(binaryRichText, 0, binaryRichText.Length, binaryRichText, 0);
            byte[] binaryHtml = Encoding.Unicode.GetBytes(htmlText);
            md5.TransformFinalBlock(binaryHtml, 0, binaryHtml.Length);
            string hash = Convert.ToBase64String(md5.Hash);
            bool used = false;

            string sql = "SELECT Id, Used FROM Clips Where Hash = @Hash";
            SQLiteCommand commandSelect = new SQLiteCommand(sql, m_dbConnection);
            commandSelect.Parameters.Add("@Hash", DbType.String).Value = hash;
            using (SQLiteDataReader reader = commandSelect.ExecuteReader())
            {
                if (reader.Read())
                {
                    used = reader.GetBoolean(reader.GetOrdinal("Used"));
                    sql = "DELETE FROM Clips Where Id = @Id";
                    SQLiteCommand commandDelete = new SQLiteCommand(sql, m_dbConnection);
                    commandDelete.Parameters.Add("@Id", DbType.String).Value = reader.GetInt32(reader.GetOrdinal("Id"));
                    commandDelete.ExecuteNonQuery();
                }
            }

            sql = "insert into Clips (Id, Title, Text, Application, Window, Created, Type, Binary, ImageSample, Size, Chars, RichText, HtmlText, Used, Url, Hash) "
               + "values (@Id, @Title, @Text, @Application, @Window, @Created, @Type, @Binary, @ImageSample, @Size, @Chars, @RichText, @HtmlText, @Used, @Url, @Hash)";

            SQLiteCommand commandInsert = new SQLiteCommand(sql, m_dbConnection);
            commandInsert.Parameters.Add("@Id", DbType.Int32).Value = LastId;
            commandInsert.Parameters.Add("@Title", DbType.String).Value = clipTitle;
            commandInsert.Parameters.Add("@Text", DbType.String).Value = plainText;
            commandInsert.Parameters.Add("@RichText", DbType.String).Value = richText;
            commandInsert.Parameters.Add("@HtmlText", DbType.String).Value = htmlText;
            commandInsert.Parameters.Add("@Application", DbType.String).Value = applicationText;
            commandInsert.Parameters.Add("@Window", DbType.String).Value = windowText;
            commandInsert.Parameters.Add("@Created", DbType.DateTime).Value = created;
            commandInsert.Parameters.Add("@Type", DbType.String).Value = typeText;
            commandInsert.Parameters.Add("@Binary", DbType.Binary).Value = binaryBuffer;
            commandInsert.Parameters.Add("@ImageSample", DbType.Binary).Value = imageSampleBuffer;
            commandInsert.Parameters.Add("@Size", DbType.Int32).Value = byteSize;
            commandInsert.Parameters.Add("@Chars", DbType.Int32).Value = chars;
            commandInsert.Parameters.Add("@Used", DbType.Boolean).Value = used;
            commandInsert.Parameters.Add("@Url", DbType.String).Value = url;
            commandInsert.Parameters.Add("@Hash", DbType.String).Value = hash;
            commandInsert.ExecuteNonQuery();

            //dbDataSet.ClipsDataTable ClipsTable = (dbDataSet.ClipsDataTable)clipBindingSource.DataSource;
            //dbDataSet.ClipsRow NewRow = (dbDataSet.ClipsRow) ClipsTable.NewRow();
            //NewRow.Id = LastId;
            //NewRow.Title = Title;
            //NewRow.Text = Text;
            //NewRow.RichText = RichText;
            //NewRow.HtmlText = HtmlText;
            //NewRow.Application = Application;
            //NewRow.Window = Window;
            //NewRow.Created = Created;
            //NewRow.Type = Type;
            //NewRow.Binary = BinaryBuffer;
            //NewRow.Size = Size;
            //NewRow.Chars = Chars;
            //NewRow.Used = false;
            //NewRow.Url = Url;
            //NewRow.Hash = Hash;
            //foreach (DataColumn Column in dbDataSet.Clips.Columns)
            //{
            //    if (Column.DataType == System.Type.GetType("System.String") && Column.MaxLength > 0)
            //    {
            //        string NewValue = NewRow[Column.ColumnName] as string;
            //        NewRow[Column.ColumnName] = NewValue.Substring(0, Math.Min(NewValue.Length, Column.MaxLength));
            //    }
            //}
            ////dbDataSet.Clips.Rows.Add(NewRow);
            ////clipsTableAdapter.Insert(NewRow.Type, NewRow.Text, NewRow.Title, NewRow.Application, NewRow.Window, NewRow.Size, NewRow.Chars, NewRow.Created, NewRow.Binary, NewRow.RichText, NewRow.Id, NewRow.HtmlText, NewRow.Used);
            //ClipsTable.Rows.InsertAt(NewRow, 0);
            //PrepareTableGrid();

            ClipsNumber++;
            int numberOfClipsToDelete = ClipsNumber - Properties.Settings.Default.HistoryDepthNumber;
            if (numberOfClipsToDelete > 0)
            {
                commandInsert.CommandText = "Delete From Clips where (NOT Favorite OR Favorite IS NULL) AND Id IN (Select ID From Clips ORDER BY ID Limit @Number)";
                commandInsert.Parameters.Add("Number", DbType.Int32).Value = numberOfClipsToDelete;
                commandInsert.ExecuteNonQuery();
                ClipsNumber -= numberOfClipsToDelete;
            }
            //if (this.Visible)
            //{
                UpdateClipBindingSource();
            //}
        }

        private static string TextClipTitle(string text)
        {
            string title = text.TrimStart();
            title = Regex.Replace(title, @"\s+", " ");
            if (title.Length > 50)
            {
                title = title.Substring(0, 50 - 1 - 3) + "...";
            }

            return title;
        }

        private void Delete_Click(object sender, EventArgs e)
        {
            allowRowLoad = false;
            //int i = dataGridView.CurrentRow.Index;
            string sql = "Delete from Clips where Id IN(null";
            SQLiteCommand command = new SQLiteCommand("", m_dbConnection);
            int counter = 0;
            foreach (DataGridViewRow selectedRow in dataGridView.SelectedRows)
            {
                DataRowView dataRow = (DataRowView)selectedRow.DataBoundItem;
                string parameterName = "@Id" + counter;
                sql += "," + parameterName;
                command.Parameters.Add(parameterName, DbType.Int32).Value = dataRow["Id"];
                counter++;
                dataGridView.Rows.Remove(selectedRow);
                ClipsNumber--;
            }
            sql += ")";
            command.CommandText = sql;
            command.ExecuteNonQuery();
            //dataGridView.ClearSelection();
            //if (i+1 < dataGridView.Rows.Count)
            //    dataGridView.CurrentCell = dataGridView.Rows[i+1].Cells[0];
            //else if (i-1 >= 0)
            //    dataGridView.CurrentCell = dataGridView.Rows[i-1].Cells[0];
            //UpdateClipBindingSource();
            allowRowLoad = true;
            AfterRowLoad();
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("User32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);
        [DllImport("User32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        static extern int UnhookWinEvent(IntPtr hWinEventHook);
        [DllImport("User32.dll")]
        static extern bool PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("User32.dll")]
        static extern short GetAsyncKeyState(Keys vKey);
        [DllImport("user32.dll")]
        static extern bool EnableWindow(IntPtr hwnd, bool bEnable);
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);
        [DllImport("user32.dll")]
        static extern IntPtr GetFocus();
        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr SetActiveWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);
        enum GetWindowCmd : uint
        {
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4,
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6
        }

        // http://stackoverflow.com/questions/37291533/change-keyboard-layout-from-c-sharp-code-with-net-4-5-2
        internal sealed class KeyboardLayout
        {
            [DllImport("user32.dll")]
            static extern uint LoadKeyboardLayout(StringBuilder pwszKLID, uint flags);
            [DllImport("user32.dll")]
            static extern uint GetKeyboardLayout(uint idThread);
            [DllImport("user32.dll")]
            static extern uint ActivateKeyboardLayout(uint hkl, uint flags);

            private readonly uint hkl;

            static class KeyboardLayoutFlags
            {
                //https://msdn.microsoft.com/ru-ru/library/windows/desktop/ms646305(v=vs.85).aspx
                public const uint KLF_ACTIVATE = 0x00000001;
                public const uint KLF_SUBSTITUTE_OK = 0x00000002;
                public const uint KLF_SETFORPROCESS = 0x00000100;
            }

            private KeyboardLayout(CultureInfo cultureInfo)
            {
                string layoutName = cultureInfo.LCID.ToString("x8");

                var pwszKlid = new StringBuilder(layoutName);
                this.hkl = LoadKeyboardLayout(pwszKlid, KeyboardLayoutFlags.KLF_ACTIVATE | KeyboardLayoutFlags.KLF_SUBSTITUTE_OK);
            }

            private KeyboardLayout(uint hkl)
            {
                this.hkl = hkl;
            }

            public uint Handle
            {
                get
                {
                    return this.hkl;
                }
            }

            public static KeyboardLayout GetCurrent()
            {
                uint hkl = GetKeyboardLayout((uint)Thread.CurrentThread.ManagedThreadId);
                return new KeyboardLayout(hkl);
            }

            public static KeyboardLayout Load(CultureInfo culture)
            {
                return new KeyboardLayout(culture);
            }

            public void Activate()
            {
                ActivateKeyboardLayout(this.hkl, KeyboardLayoutFlags.KLF_SETFORPROCESS);
            }
        }

        //class KeyboardLayoutScope : IDisposable
        //{
        //    private readonly KeyboardLayout currentLayout;

        //    public KeyboardLayoutScope(CultureInfo culture)
        //    {
        //        this.currentLayout = KeyboardLayout.GetCurrent();
        //        var layout = KeyboardLayout.Load(culture);
        //        layout.Activate();
        //    }

        //    public void Dispose()
        //    {
        //        this.currentLayout.Activate();
        //    }
        //}

        private string CopyClipToClipboard(/*out DataObject oldDataObject,*/ bool onlySelectedPlainText = false)
        {
            //oldDataObject = null;
            StringCollection lastFilterValues = Properties.Settings.Default.LastFilterValues;
            if (filterText != "" && !lastFilterValues.Contains(filterText))
            {
                lastFilterValues.Insert(0, filterText);
                while (lastFilterValues.Count > 20)
                {
                    lastFilterValues.RemoveAt(lastFilterValues.Count - 1);
                }
                FillFilterItems();
            }

            //DataRow CurrentDataRow = ((DataRowView)clipBindingSource.Current).Row;
            string type = (string)RowReader["type"];
            object richText = RowReader["RichText"];
            object htmlText = RowReader["HtmlText"];
            byte[] binary = RowReader["Binary"] as byte[];
            string clipText = (string)RowReader["Text"];
            bool selectedPlainTextMode = onlySelectedPlainText && richTextBox.SelectedText != "";
            if (selectedPlainTextMode)
            {
                clipText = richTextBox.SelectedText; // Если тут не копировать, а передавать SelectedText, то возникает долгое ожидание потом
            }
            DataObject dto = new DataObject();
            if (IsTextType(type))
            {
                dto.SetText(clipText, TextDataFormat.UnicodeText);
            }
            if (type == "rtf" && !(richText is DBNull) && !onlySelectedPlainText)
            {
                dto.SetText((string)richText, TextDataFormat.Rtf);
            }
            if (type == "html" && !(htmlText is DBNull) && !onlySelectedPlainText)
            {
                dto.SetText((string)htmlText, TextDataFormat.Html);
            }
            if (type == "file" && !onlySelectedPlainText)
            {
                string[] fileNameList = clipText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                StringCollection fileNameCollection = new StringCollection();
                foreach (string fileName in fileNameList)
                {
                    fileNameCollection.Add(fileName);
                }
                dto.SetFileDropList(fileNameCollection);
            }
            if (type == "img" && !onlySelectedPlainText)
            {
                Image image = GetImageFromBinary(binary);
                dto.SetImage(image);
                //MemoryStream ms = new MemoryStream();
                //MemoryStream ms2 = new MemoryStream();
                //image.Save(ms, ImageFormat.Bmp);
                //byte[] b = ms.GetBuffer();
                //ms2.Write(b, 14, (int)ms.Length - 14);
                //ms.Position = 0;
                //dto.SetData("DeviceIndependentBitmap", ms2);
            }
            //if (!Properties.Settings.Default.MoveCopiedClipToTop)
            //    CaptureClipboard = false;
            ////oldDataObject = (DataObject) Clipboard.GetDataObject();
            RemoveClipboardFormatListener(this.Handle);
            Clipboard.Clear();
            Clipboard.SetDataObject(dto, true); // Very important to set second parameter to true to give immidiate access to buffer to other processes!
            ConnectClipboard();
            //Application.DoEvents(); // To process UpdateClipBoardMessage
            ////if (CaptureClipboard)
            ////    GotoLastRow();
            //CaptureClipboard = true;
            return clipText;
        }

        private static bool IsTextType(string type)
        {
            return type == "rtf" || type == "text" || type == "html";
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private void SendPaste(PasteMethod pasteMethod = PasteMethod.Standart)
        {
            if (dataGridView.CurrentRow == null)
                return;
            string textToPaste = "";
            //DataObject oldDataObject;
            //if (pasteMethod != PasteMethod.SendChars)
            textToPaste = CopyClipToClipboard(/*out oldDataObject,*/ pasteMethod != PasteMethod.Standart);
            //CultureInfo EnglishCultureInfo = null;
            //foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
            //{
            //    if (String.Compare(lang.Culture.TwoLetterISOLanguageName, "en", true) == 0)
            //    {
            //        EnglishCultureInfo = lang.Culture;
            //        break;
            //    }
            //}
            //if (EnglishCultureInfo == null)
            //{
            //    MessageBox.Show(this, "Unable to find English input language", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}
            int targetProcessId;
            uint remoteThreadId = GetWindowThreadProcessId(lastActiveWindow, out targetProcessId);
            if (targetProcessId != 0 && !UacHelper.IsProcessAccessible(targetProcessId))
            {
                MessageBox.Show(this, CurrentLangResourceManager.GetString("CantPasteInElevatedWindow"), Application.ProductName);
                return;
            }
            
            // not reliable method
            // Previous active window by z-order https://www.whitebyte.info/programming/how-to-get-main-window-handle-of-the-last-active-window

            if (!this.TopMost)
            {
                this.Close();
            }
            else
            {
                SetForegroundWindow(lastActiveWindow);
                Debug.WriteLine("Set foreground window " + lastActiveWindow + " " + GetWindowTitle(lastActiveWindow));
            }
            int waitStep = 5;
            IntPtr hForegroundWindow = IntPtr.Zero;
            for (int i = 0; i < 500; i += waitStep)
            {
                hForegroundWindow = GetForegroundWindow();
                if (hForegroundWindow != IntPtr.Zero)
                    break;
                Thread.Sleep(waitStep);
            }
            Debug.WriteLine("Get foreground window " + hForegroundWindow + " " + GetWindowTitle(hForegroundWindow));
            //IntPtr hFocusWindow = FocusWindow();
            //string WindowTitle = GetWindowTitle(hFocusWindow);
            //Debug.WriteLine("Window = " + hFocusWindow + " \"" + WindowTitle + "\"");
            //Thread.Sleep(50);

            //AttachThreadInput(GetCurrentThreadId(), remoteThreadId, true);
            //IntPtr hFocusWindow = IntPtr.Zero;
            //for (int i = 0; i < 500; i+= waitStep)
            //{
            //    hFocusWindow = GetFocus();
            //    //var guiInfo = new GUITHREADINFO();
            //    //guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
            //    //GetGUIThreadInfo(remoteThreadId, out guiInfo);
            //    if (hFocusWindow != IntPtr.Zero)
            //    {
            //        //Debug.WriteLine("Active " + guiInfo.hwndActive);
            //        //Debug.WriteLine("Caret " + guiInfo.hwndCaret);
            //        SetActiveWindow(hFocusWindow);
            //        break;
            //    }
            //    Thread.Sleep(waitStep);
            //}
            //Debug.WriteLine("Got focus window " + hFocusWindow + " " + GetWindowTitle(hFocusWindow));

            InputSimulator inputSimulator = new InputSimulator(); // http://inputsimulator.codeplex.com/
            if (pasteMethod != PasteMethod.SendChars)
            {
                // Spyed from AceText. Works in all windows including CMD and RDP
                const int KEYEVENTF_KEYUP = 0x0002; //Key up flag

                // Release all key modifiers
                keybd_event((byte)VirtualKeyCode.SHIFT, 0x2A, KEYEVENTF_KEYUP, 0);
                keybd_event((byte)VirtualKeyCode.SHIFT, 0x36, KEYEVENTF_KEYUP, 0);
                keybd_event((byte)VirtualKeyCode.CONTROL, 0x1D, KEYEVENTF_KEYUP, 0);
                keybd_event((byte)VirtualKeyCode.MENU, 0x38, KEYEVENTF_KEYUP, 0); // LEFT
                keybd_event((byte)VirtualKeyCode.LWIN, 0x5B, KEYEVENTF_KEYUP, 0);
                keybd_event((byte)VirtualKeyCode.RWIN, 0x5C, KEYEVENTF_KEYUP, 0);

                // Send CTLR+V
                keybd_event((byte)VirtualKeyCode.CONTROL, 0x1D, 0, 0);
                keybd_event((byte)'V', 0x2f, 0, 0);
                keybd_event((byte)'V', 0x2f, KEYEVENTF_KEYUP, 0);
                keybd_event((byte)VirtualKeyCode.CONTROL, 0x1D, KEYEVENTF_KEYUP, 0);
            }
            else
            {
                string type = (string)RowReader["type"];
                if (!IsTextType(type))
                    return;
                //{
                    inputSimulator.Keyboard.TextEntry(textToPaste);
                //}
                //catch (Exception error)
                //{
                //    MessageBox.Show(this, error.Message, Application.ProductName);
                //}
            }
            //AttachThreadInput(GetCurrentThreadId(), remoteThreadId, false);
            SetRowMark("Used");
            if (false
                || Properties.Settings.Default.MoveCopiedClipToTop 
                || (true 
                    && pasteMethod == PasteMethod.PasteText 
                    && richTextBox.SelectedText != ""))
            {
                GetClipboardData();
            }
            else
            {
                ((DataRowView)dataGridView.CurrentRow.DataBoundItem).Row["Used"] = true;
                //PrepareTableGrid();
                UpdateTableGridRowBackColor(dataGridView.CurrentRow);
            }

            // We need delay about 100ms before restore clipboard object
            //Clipboard.SetDataObject(oldDataObject);
        }

        private static IntPtr GetTopParentWindow(IntPtr hForegroundWindow)
        {
            while (true)
            {
                IntPtr temp = GetParent(hForegroundWindow);
                if (temp.Equals(IntPtr.Zero)) break;
                hForegroundWindow = temp;
            }

            return hForegroundWindow;
        }

        private void SetRowMark(string fieldName, bool newValue = true, bool allSelected = false)
        {
            string sql = "Update Clips set " + fieldName + "=@Value where Id IN(null";
            SQLiteCommand command = new SQLiteCommand("", m_dbConnection);
            List<DataGridViewRow> selectedRows = new List<DataGridViewRow>();
            if (allSelected)
                foreach (DataGridViewRow selectedRow in dataGridView.SelectedRows)
                    selectedRows.Add(selectedRow);
            else
                selectedRows.Add(dataGridView.CurrentRow);
            int counter = 0;
            ReadFilterText();
            foreach (DataGridViewRow selectedRow in selectedRows)
            {
                if (selectedRow == null)
                    continue;
                DataRowView dataRow = (DataRowView)selectedRow.DataBoundItem;
                string parameterName = "@Id" + counter;
                sql += "," + parameterName;
                command.Parameters.Add(parameterName, DbType.Int32).Value = dataRow["Id"];
                counter++;
                dataRow[fieldName] = newValue;
                //PrepareRow(selectedRow);
            }
            sql += ")";
            command.CommandText = sql;
            command.Parameters.Add("@Value", DbType.Boolean).Value = newValue;
            command.ExecuteNonQuery();

            ////dbDataSet.ClipsRow Row = (dbDataSet.ClipsRow)dbDataSet.Clips.Rows[dataGridView.CurrentRow.Index];
            ////Row[fieldName] = newValue;
            ////dataAdapter.Update(dbDataSet);
            //UpdateClipBindingSource();
        }

        private void ReadFilterText()
        {
            filterText = comboBoxFilter.Text;
        }

        private static Image GetImageFromBinary(byte[] binary)
        {
            MemoryStream memoryStream = new MemoryStream(binary, 0, binary.Length);
            memoryStream.Write(binary, 0, binary.Length);
            Image image = new Bitmap(memoryStream);
            return image;
        }

        private void FillFilterItems()
        {
            StringCollection lastFilterValues = Properties.Settings.Default.LastFilterValues;
            comboBoxFilter.Items.Clear();
            foreach (string String in lastFilterValues)
            {
                comboBoxFilter.Items.Add(String);
            }
        }

        IntPtr GetFocusWindow(int maxWait = 100)
        {
            IntPtr hwnd = GetForegroundWindow();
            int pid;
            uint remoteThreadId = GetWindowThreadProcessId(hwnd, out pid);
            uint currentThreadId = GetCurrentThreadId();
            //AttachTrheadInput is needed so we can get the handle of a focused window in another app
            AttachThreadInput(remoteThreadId, currentThreadId, true);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            while (stopWatch.ElapsedMilliseconds < maxWait)
            {
                hwnd = GetFocus();
                if (hwnd != IntPtr.Zero)
                    break;
                Thread.Sleep(5);
            }
            AttachThreadInput(remoteThreadId, currentThreadId, false);
            return hwnd;
        }

        void GetClipboardOwnerInfo(out string window, out string application)
        {
            IntPtr hwnd = GetClipboardOwner();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = lastActiveWindow;
            }
            int processId;
            GetWindowThreadProcessId(hwnd, out processId);
            Process process1 = Process.GetProcessById(processId);
            application = process1.ProcessName;
            hwnd = process1.MainWindowHandle;
            window = GetWindowTitle(hwnd);
            //// We need top level window
            ////const uint GW_OWNER = 4;
            //while ((int)hwnd != 0)
            //{
            //    Window = GetWindowTitle(hwnd);
            //    //IntPtr hOwner = GetWindow(hwnd, GW_OWNER);
            //    hwnd = GetParent(hwnd);
            //    //if ((int) hwnd == 0)
            //    //{
            //    //    hwnd = hOwner;
            //    //}
            //}
        }

        void sendKey(IntPtr hwnd, Keys keyCode, bool extended = false, bool down = true, bool up = true)
        {
            // http://stackoverflow.com/questions/10280000/how-to-create-lparam-of-sendmessage-wm-keydown
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            uint scanCode = MapVirtualKey((uint)keyCode, 0);
            uint lParam = 0x00000001 | (scanCode << 16);
            if (extended)
            {
                lParam |= 0x01000000;
            }
            if (down)
            {
                PostMessage(hwnd, WM_KEYDOWN, (int)keyCode, (int)lParam);
            }
            lParam |= 0xC0000000;  // set previous key and transition states (bits 30 and 31)
            if (up)
            {
                PostMessage(hwnd, WM_KEYUP, (int)keyCode, (int)lParam);
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            string windowTitle = "";
            if (GetWindowText(hwnd, buff, nChars) > 0)
            {
                windowTitle = buff.ToString();
            }
            return windowTitle;
        }

        private void dataGridView_DoubleClick(object sender, EventArgs e)
        {
            SendPaste();
        }
        private void pasteOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendPaste();
        }
        private void pasteAsTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendPaste(PasteMethod.PasteText);
        }
        private void dataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (allowRowLoad)
            {
                if (EditMode)
                    editClipTextToolStripMenuItem_Click();
                else
                    AfterRowLoad();
            }
        }

        private void SaveClipText()
        {
            string sql = "Update Clips set Title = @Title, Text = @Text where Id = @Id";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add("@Id", DbType.Int32).Value = RowReader["Id"];
            command.Parameters.Add("@Text", DbType.String).Value = richTextBox.Text;
            string newTitle = "";
            if (RowReader["Title"].ToString() == TextClipTitle(RowReader["Text"].ToString()))
                newTitle = TextClipTitle(richTextBox.Text);
            else
                newTitle = RowReader["Title"].ToString();
            command.Parameters.Add("@Title", DbType.String).Value = newTitle;
            command.ExecuteNonQuery();
        }

        private void Main_Deactivate(object sender, EventArgs e)
        {
            //if (this.WindowState == FormWindowState.Minimized)
            //{
            //    this.ShowInTaskbar = false;
            //    //notifyIcon.Visible = true;
            //}
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            { ShowForPaste(); }
        }

        [DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo")]
        public static extern bool GetGUIThreadInfo(uint tId, out GUITHREADINFO threadInfo);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, out Point position);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr handle, out RECT lpRect);

        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool GetClientRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32")]
        private extern static int GetCaretPos(out Point p);

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
            //public System.Drawing.Rectangle rcCaret;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private void ShowForPaste()
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();

            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            //AutoGotoLastRow = Properties.Settings.Default.SelectTopClipOnShow;
            if (Properties.Settings.Default.WindowAutoPosition)
            {
                // https://www.codeproject.com/Articles/34520/Getting-Caret-Position-Inside-Any-Application
                // http://stackoverflow.com/questions/31055249/is-it-possible-to-get-caret-position-in-word-to-update-faster
                //IntPtr hWindow = GetForegroundWindow();
                IntPtr hWindow = lastActiveWindow;
                if (hWindow != this.Handle)
                { 
	                int pid;
	                uint remoteThreadId = GetWindowThreadProcessId(hWindow, out pid);
	                var guiInfo = new GUITHREADINFO();
	                guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
	                GetGUIThreadInfo(remoteThreadId, out guiInfo);
	                Point point = new Point(0, 0);
                    ClientToScreen(guiInfo.hwndCaret, out point);
                    //AttachThreadInput(GetCurrentThreadId(), remoteThreadId, true);
                    //int Result = GetCaretPos(out point);
                    //AttachThreadInput(GetCurrentThreadId(), remoteThreadId, false);
                    // Screen.FromHandle(hwnd)
                    RECT activeRect;
                    if (point.Y > 0)
	                {
                        activeRect = guiInfo.rcCaret;
                        this.Left = Math.Min(activeRect.right + point.X, SystemInformation.VirtualScreen.Width - this.Width);
                        this.Top = Math.Min(activeRect.bottom + point.Y + 1, SystemInformation.VirtualScreen.Height - this.Height - 30);
                    }
                    else
                    {
                        IntPtr baseWindow;
                        if (guiInfo.hwndFocus != IntPtr.Zero)
                            baseWindow = guiInfo.hwndFocus;
                        else
                            baseWindow = hWindow;
                        ClientToScreen(baseWindow, out point);
                        GetWindowRect(baseWindow, out activeRect);
                        this.Left = Math.Max(0, Math.Min((activeRect.right - activeRect.left - this.Width) / 2 + point.X, SystemInformation.VirtualScreen.Width - this.Width));
                        this.Top = Math.Max(0, Math.Min((activeRect.bottom - activeRect.top - this.Height) / 2 + point.Y, SystemInformation.VirtualScreen.Height - this.Height - 30));
                    }
                }
            }
            //sw.Stop();
            //Debug.WriteLine("autoposition duration" + sw.ElapsedMilliseconds.ToString());
            this.Activate();
            this.Visible = true;
            //notifyIcon.Visible = false;
            if (Properties.Settings.Default.SelectTopClipOnShow)
                GotoLastRow();
        }

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                this.Close();
            }
            if (e.KeyCode == Keys.Enter)
            {
                PasteMethod pasteMethod;
                if (e.Control)
                    pasteMethod = PasteMethod.PasteText;
                else
                {
                    if (!pasteENTERToolStripMenuItem.Enabled)
                        return;
                    pasteMethod = PasteMethod.Standart;
                }
                SendPaste(pasteMethod);
                e.Handled = true;
            }
        }

        private void exitToolStripMenuItem_Click(object sender = null, EventArgs e = null)
        {
            AllowFormClose = true;
            this.Close();
        }

        private void Filter_KeyDown(object sender, KeyEventArgs e)
        {
            PassKeyToGrid(true, e);
        }

        private void Filter_KeyUp(object sender, KeyEventArgs e)
        {
            PassKeyToGrid(false, e);
        }

        private void PassKeyToGrid(bool downOrUp, KeyEventArgs e)
        {
            if (IsKeyPassedFromFilterToGrid(e.KeyCode, e.Control))
            {
                sendKey(dataGridView.Handle, e.KeyCode, false, downOrUp, !downOrUp);
                e.Handled = true;
            }
        }

        private static bool IsKeyPassedFromFilterToGrid(Keys key, bool isCtrlDown = false)
        {
            return false
                || key == Keys.Down
                || key == Keys.Up
                || key == Keys.PageUp
                || key == Keys.PageDown
                || key == Keys.ControlKey
                || key == Keys.Home && isCtrlDown
                || key == Keys.End && isCtrlDown;
        }

        //protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        //{
        //    if (keyData == (Keys.Control | Keys.F9))
        //    {
        //        ClearFilter_Click();
        //        return true;
        //    }
        //    return base.ProcessCmdKey(ref msg, keyData);
        //}

        private void dataGridView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearFilter_Click();
        }

        private void gotoLastToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GotoLastRow();
        }

        void GotoLastRow()
        {
            if (dataGridView.Rows.Count > 0)
            {
                clipBindingSource.MoveFirst();
                SelectCurrentRow();
            }
            AfterRowLoad();
        }

        void SelectCurrentRow(bool forceRowLoad = false)
        {
            dataGridView.ClearSelection();
            if (dataGridView.CurrentRow == null)
            {
                GotoLastRow();
                return;
            }
            dataGridView.Rows[dataGridView.CurrentRow.Index].Selected = true;
            if (forceRowLoad)
                AfterRowLoad();
        }

        private void activateListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView.Focus();
        }

        private void PrepareTableGrid()
        {
            //ReadFilterText();
            //foreach (DataGridViewRow row in dataGridView.Rows)
            //{
            //    PrepareRow(row);
            //}
            //dataGridView.Update();
        }

        private void PrepareRow(DataGridViewRow row)
        {
            DataRowView dataRowView = (DataRowView) row.DataBoundItem;
            int shortSize = dataRowView.Row["Chars"].ToString().Length;
            if (shortSize > 2)
                row.Cells["VisualWeight"].Value = shortSize;
            string clipType = (string) dataRowView.Row["Type"];
            Bitmap image = null;
            switch (clipType)
            {
                case "text":
                    image = imageText;
                    break;
                case "html":
                    image = imageHtml;
                    break;
                case "rtf":
                    image = imageRtf;
                    break;
                case "file":
                    image = imageFile;
                    break;
                case "img":
                    image = imageImg;
                    break;
                default:
                    break;
            }
            if (image != null)
            {
                row.Cells["TypeImg"].Value = image;
            }
            row.Cells["Title"].Value = dataRowView.Row["Title"].ToString();
            if (filterText != "" && dataGridView.Columns["Title"].Visible)
            {
                _richTextBox.Clear();
                _richTextBox.Text = row.Cells["Title"].Value.ToString();
                MatchCollection tempMatches;
                MarkRegExpMatchesInRichTextBox(_richTextBox, Regex.Escape(filterText).Replace("%", ".*?"), Color.Red,
                    false, out tempMatches);
                row.Cells["Title"].Value = _richTextBox.Rtf;
            }
            if (dataGridView.Columns["Title"].Visible)
            {
                var imageSampleBuffer = dataRowView["ImageSample"];
                if (imageSampleBuffer != DBNull.Value)
                    if ((imageSampleBuffer as byte[]).Length > 0)
                    {
                        Image imageSample = GetImageFromBinary((byte[])imageSampleBuffer);
                        row.Cells["imageSample"].Value = imageSample;
                        ////string str = BitConverter.ToString((byte[])imageSampleBuffer, 0).Replace("-", string.Empty);
                        ////string imgString = @"{\pict\pngblip\picw" + imageSample.Width + @"\pich" + imageSample.Height + @"\picwgoal" + imageSample.Width + @"\pichgoal" + imageSample.Height + @"\bin " + str + "}";
                        //string imgString = GetEmbedImageString((Bitmap)imageSample, 0, 18);
                        //_richTextBox.SelectionStart = _richTextBox.TextLength;
                        //_richTextBox.SelectedRtf = imgString;
                    }

            }
            UpdateTableGridRowBackColor(row);
        }

        public string GetImageForRTF(Image img, int width = 0, int height = 0)
        {
            //string newPath = Path.Combine(Environment.CurrentDirectory, path);
            //Image img = Image.FromFile(newPath);
            if (width == 0)
                width = img.Width;
            if (height == 0)
                height = img.Width;
            MemoryStream stream = new MemoryStream();
            img.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bytes = stream.ToArray();
            string str = BitConverter.ToString(bytes, 0).Replace("-", string.Empty);
            //string str = System.Text.Encoding.UTF8.GetString(bytes);
            string mpic = @"{\pict\wbitmapN\picw" + img.Width + @"\pich" + img.Height + @"\picwgoal" + width + @"\pichgoal" + height + @"\bin " + str + "}";
            return mpic;
        }

        // RTF Image Format
        // {\pict\wmetafile8\picw[A]\pich[B]\picwgoal[C]\pichgoal[D]
        //  
        // A    = (Image Width in Pixels / Graphics.DpiX) * 2540 
        //  
        // B    = (Image Height in Pixels / Graphics.DpiX) * 2540 
        //  
        // C    = (Image Width in Pixels / Graphics.DpiX) * 1440 
        //  
        // D    = (Image Height in Pixels / Graphics.DpiX) * 1440 

        [Flags]
        enum EmfToWmfBitsFlags
        {
            EmfToWmfBitsFlagsDefault = 0x00000000,
            EmfToWmfBitsFlagsEmbedEmf = 0x00000001,
            EmfToWmfBitsFlagsIncludePlaceable = 0x00000002,
            EmfToWmfBitsFlagsNoXORClip = 0x00000004
        }

        const int MM_ISOTROPIC = 7;
        const int MM_ANISOTROPIC = 8;

        [DllImport("gdiplus.dll")]
        private static extern uint GdipEmfToWmfBits(IntPtr _hEmf, uint _bufferSize,
            byte[] _buffer, int _mappingMode, EmfToWmfBitsFlags _flags);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SetMetaFileBitsEx(uint _bufferSize,
            byte[] _buffer);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CopyMetaFile(IntPtr hWmf,
            string filename);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteMetaFile(IntPtr hWmf);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteEnhMetaFile(IntPtr hEmf);

        public static string GetEmbedImageString(Bitmap image, int width = 0, int height = 0)
        {
            Metafile metafile = null;
            float dpiX; float dpiY;
            if (height == 0 || height > image.Height)
                height = image.Height;
            if (width == 0 || width > image.Width)
                width = image.Width;
            using (Graphics g = Graphics.FromImage(image))
            {
                IntPtr hDC = g.GetHdc();
                metafile = new Metafile(hDC, EmfType.EmfOnly);
                g.ReleaseHdc(hDC);
            }

            using (Graphics g = Graphics.FromImage(metafile))
            {
                g.DrawImage(image, 0, 0);
                dpiX = g.DpiX;
                dpiY = g.DpiY;
            }

            IntPtr _hEmf = metafile.GetHenhmetafile();
            uint _bufferSize = GdipEmfToWmfBits(_hEmf, 0, null, MM_ANISOTROPIC,
                EmfToWmfBitsFlags.EmfToWmfBitsFlagsDefault);
            byte[] _buffer = new byte[_bufferSize];
            GdipEmfToWmfBits(_hEmf, _bufferSize, _buffer, MM_ANISOTROPIC,
                                        EmfToWmfBitsFlags.EmfToWmfBitsFlagsDefault);
            IntPtr hmf = SetMetaFileBitsEx(_bufferSize, _buffer);
            string tempfile = Path.GetTempFileName();
            CopyMetaFile(hmf, tempfile);
            DeleteMetaFile(hmf);
            DeleteEnhMetaFile(_hEmf);

            var stream = new MemoryStream();
            byte[] data = File.ReadAllBytes(tempfile);
            //File.Delete (tempfile);
            int count = data.Length;
            stream.Write(data, 0, count);

            string proto = @"{\rtf1{\pict\wmetafile8\picw" + (int)(((float)width / dpiX) * 2540)
                              + @"\pich" + (int)(((float)height / dpiY) * 2540)
                              + @"\picwgoal" + (int)(((float)width / dpiX) * 1440)
                              + @"\pichgoal" + (int)(((float) height / dpiY) * 1440)
                              + " "
                  + BitConverter.ToString(stream.ToArray()).Replace("-", "")
                              + "}}";
            return proto;
        }

        private void MergeCellsInRow(DataGridView dataGridView1, DataGridViewRow row, int col1, int col2)
        {
            Graphics g = dataGridView1.CreateGraphics();
            Pen p = new Pen(dataGridView1.GridColor);
            Rectangle r1 = dataGridView1.GetCellDisplayRectangle(col1, row.Index, true);
            //Rectangle r2 = dataGridView1.GetCellDisplayRectangle(col2, row.Index, true);

            int recWidth = 0;
            string recValue = string.Empty;
            for (int i = col1; i <= col2; i++)
            {
                if (!row.Cells[i].Visible)
                    continue;
                recWidth += dataGridView1.GetCellDisplayRectangle(i, row.Index, true).Width;
                if (row.Cells[i].Value != null)
                    recValue += row.Cells[i].Value.ToString() + " ";
            }
            Rectangle newCell = new Rectangle(r1.X, r1.Y, recWidth, r1.Height);
            g.FillRectangle(new SolidBrush(dataGridView1.DefaultCellStyle.BackColor), newCell);
            g.DrawRectangle(p, newCell);
            g.DrawString(recValue, dataGridView1.DefaultCellStyle.Font, new SolidBrush(dataGridView1.DefaultCellStyle.ForeColor), newCell.X + 3, newCell.Y + 3);
        }

        public static Image CopyRectImage(Image image, Rectangle selection)
        {
            int newBottom = selection.Bottom;
            if (selection.Bottom > image.Height)
                newBottom = image.Height;
            int newRight = selection.Right;
            if (selection.Right > image.Width)
                newRight = image.Width;
            // TODO check other borders
            Bitmap RectImage = (image as Bitmap).Clone(new Rectangle(selection.Left, selection.Top, newRight, newBottom), image.PixelFormat);
            return RectImage;
        }

        private void UpdateTableGridRowBackColor(DataGridViewRow row)
        {
            DataRowView dataRowView = (DataRowView)(row.DataBoundItem);
            var favVal = dataRowView.Row["Favorite"];
            if (favVal != DBNull.Value && (bool)favVal)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.BackColor = favouriteColor;
                }
            }
            else if ((bool)dataRowView.Row["Used"])
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.BackColor = _usedColor;
                }
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings settingsForm = new Settings();
            hook.UnregisterHotKeys();
            settingsForm.ShowDialog(this);
            if (settingsForm.DialogResult == DialogResult.OK)
                LoadSettings();
            RegisterHotKeys();
        }

        private class ListItemNameText
        {
            public string Name { get; set; }
            public string Text { get; set; }
        }

        private void LoadSettings()
        {
            UpdateControlsStates();
            UpdateCurrentCulture();
            cultureManager1.UICulture = Thread.CurrentThread.CurrentUICulture;

            this.Text = Application.ProductName + " " + Properties.Resources.Version;

            BindingList<ListItemNameText> _comboItemsTypes = new BindingList<ListItemNameText>
            {
                new ListItemNameText {Name = "allTypes", Text = CurrentLangResourceManager.GetString("allTypes")},
                new ListItemNameText {Name = "text", Text = CurrentLangResourceManager.GetString("text")},
                new ListItemNameText {Name = "file", Text = CurrentLangResourceManager.GetString("file")},
                new ListItemNameText {Name = "img", Text = CurrentLangResourceManager.GetString("img")}
            };
            TypeFilter.DataSource = _comboItemsTypes;
            TypeFilter.DisplayMember = "Text";
            TypeFilter.ValueMember = "Name";

            BindingList<ListItemNameText> _comboItemsMarks = new BindingList<ListItemNameText>();
            _comboItemsMarks.Add(new ListItemNameText { Name = "allMarks", Text = CurrentLangResourceManager.GetString("allMarks") });
            _comboItemsMarks.Add(new ListItemNameText { Name = "used", Text = CurrentLangResourceManager.GetString("used") });
            _comboItemsMarks.Add(new ListItemNameText { Name = "favorite", Text = CurrentLangResourceManager.GetString("favorite") });
            MarkFilter.DataSource = _comboItemsMarks;
            MarkFilter.DisplayMember = "Text";
            MarkFilter.ValueMember = "Name";

            ChooseTitleColumnDraw();
            AfterRowLoad();
        }

        private void ChooseTitleColumnDraw()
        {
            bool ResultSimpleDraw = Properties.Settings.Default.ClipListSimpleDraw /*|| filterText == ""*/;
            dataGridView.Columns["TitleSimple"].Visible = ResultSimpleDraw;
            dataGridView.Columns["Title"].Visible = !ResultSimpleDraw;
        }

        public async void CheckUpdate(bool UserRequest = false)
        {
            if (!UserRequest && !Properties.Settings.Default.AutoCheckForUpdate)
                return;
            buttonUpdate.Visible = false;
            toolStripUpdateToSeparator.Visible = false;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    string HtmlSource = await wc.DownloadStringTaskAsync(Properties.Resources.Website);
                    var htmlParser = new HtmlParser();
                    var documentHtml = htmlParser.Parse(HtmlSource);
                    IHtmlCollection<IElement> Refs = documentHtml.GetElementsByClassName("sfdl");
                    Match match = Regex.Match(Refs[0].TextContent, @"Clip Angel (.*).zip");
                    if (match == null)
                        return;
                    ActualVersion = match.Groups[1].Value;
                    if (ActualVersion != Properties.Resources.Version)
                    {
                        buttonUpdate.Visible = true;
                        toolStripUpdateToSeparator.Visible = true;
                        buttonUpdate.ForeColor = Color.Blue;
                        buttonUpdate.Text = CurrentLangResourceManager.GetString("UpdateTo") + " " + ActualVersion;
                        if (UserRequest)
                        {
                            MessageBox.Show(this, CurrentLangResourceManager.GetString("NewVersionAvailable"), Application.ProductName);
                        }
                    }
                    else if (UserRequest)
                    {
                        MessageBox.Show(this, CurrentLangResourceManager.GetString("YouLatestVersion"), Application.ProductName);
                    }
                }
            }
            catch
            {
                if (UserRequest)
                    throw;
            }
        }

        private void RunUpdate()
        {
            using (WebClient wc = new WebClient())
            {
                string tempFolder = Path.GetTempPath() + Guid.NewGuid();
                Directory.CreateDirectory(tempFolder);
                string tempFilenameZip = tempFolder + "\\NewVersion" + ".zip";
                bool success = true;
                //try
                //{
                    wc.DownloadFile(Properties.Resources.DownloadPage, tempFilenameZip);
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show(ex.ToString());
                //    Success = false;
                //}

                //string HtmlSource = wc.DownloadString(Properties.Resources.DownloadPage);
                //var htmlParser = new HtmlParser();
                //var documentHtml = htmlParser.Parse(HtmlSource);
                //IHtmlCollection<IElement> Refs = documentHtml.GetElementsByClassName("direct-download");
                //string DirectLink = Refs[1].GetAttribute("href");
                //wc.DownloadFile(DirectLink, TempFilename);
                string UpdaterName = "ExternalUpdater.exe";
                File.Copy(UpdaterName, tempFolder + "\\" + UpdaterName);
                File.Copy("DotNetZip.dll", tempFolder + "\\DotNetZip.dll");
                if (success)
                {
                    Process.Start(tempFolder + "\\" + UpdaterName, "\"" + tempFilenameZip + "\" \"" + Application.StartupPath + "\" \"" + Application.ExecutablePath
                        + "\" " + Process.GetCurrentProcess().Id);
                    exitToolStripMenuItem_Click();
                }
            }
        }

        private void UpdateCurrentCulture()
        {
            if (Properties.Settings.Default.Language == "Default")
                Locale = Application.CurrentCulture.TwoLetterISOLanguageName;
            else if (Properties.Settings.Default.Language == "Russian")
                Locale = "ru";
            else
                Locale = "en";
            //if (true
            //    && CurrentLangResourceManager != null
            //    && String.Compare(Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName, Locale, true) != 0)
            //{
            //    MessageBox.Show(this, CurrentLangResourceManager.GetString("LangRestart"), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            //}
            if (String.Compare(Locale, "ru", true) == 0)
                CurrentLangResourceManager = Properties.Resource_RU.ResourceManager;
            else
                CurrentLangResourceManager = Properties.Resources.ResourceManager;
            // https://www.codeproject.com/Articles/23694/Changing-Your-Application-User-Interface-Culture-O
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Locale);
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.Save();
            RemoveClipboardFormatListener(this.Handle);
            UnhookWinEvent(HookChangeActiveWindow);
        }

        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            exitToolStripMenuItem_Click();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form aboutBox = new AboutBox();
            aboutBox.ShowDialog(this);
        }

        private void Main_Activated(object sender, EventArgs e)
        {
            ////#if DEBUG
            ////    return;
            ////#endif
            ////PrepareTableGrid(); // иначе оформление не появлялось при свернутом запуске
            //UpdateClipBindingSource();

        }

        private void Filter_KeyPress(object sender, KeyPressEventArgs e)
        {
            // http://csharpcoding.org/tag/keypress/ Workaroud strange beeping 
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Escape)
                e.Handled = true;
        }

        private static void OpenLinkIfCtrlPressed(RichTextBox sender, EventArgs e, MatchCollection matches)
        {
            Keys mod = Control.ModifierKeys & Keys.Modifiers;
            bool ctrlOnly = mod == Keys.Control;
            if (ctrlOnly)
                foreach (Match match in matches)
                {
                    if (match.Index <= sender.SelectionStart && (match.Index + match.Length) >= sender.SelectionStart)
                        Process.Start(match.Value);
                }
                    
        }

        private void textBoxUrl_Click(object sender, EventArgs e)
        {
            OpenLinkIfCtrlPressed(sender as RichTextBox, e, UrlLinkMatches);
        }

        private void ImageControl_DoubleClick(object sender, EventArgs e)
        {
            OpenInDefaultApplication(); 
        }

        private void TypeFilter_SelectedValueChanged(object sender, EventArgs e)
        {
            if (AllowFilterProcessing)
            {
                UpdateClipBindingSource();
            }
        }

        private void buttonFindNext_Click(object sender, EventArgs e)
        {
            RichTextBox control = richTextBox;
            if (FilterMatches == null)
                return;
            if (TextWasCut)
                AfterRowLoad(-1, true);
            foreach (Match match in FilterMatches)
            {
                if (false
                    || control.SelectionStart < match.Index 
                    || (true
                        && control.SelectionLength == 0
                        && match.Index == 0
                        ))
                {
                    control.SelectionStart = match.Index;
                    control.SelectionLength = match.Length;
                    control.HideSelection = false;
                    break;
                }
            }
        }

        private void buttonFindPrevious_Click(object sender, EventArgs e)
        {
            RichTextBox control = richTextBox;
            if (FilterMatches == null)
                return;
            Match prevMatch = null;
            foreach (Match match in FilterMatches)
            {
                if (false
                    || control.SelectionStart > match.Index
                    || (true
                        && control.SelectionLength == 0
                        && match.Index == 0
                        ))
                {
                    prevMatch = match;
                }
            }
            if (prevMatch != null)
            {
                control.SelectionStart = prevMatch.Index;
                control.SelectionLength = prevMatch.Length;
                control.HideSelection = false;
            }
        }

        private void wordWrapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.WordWrap = !Properties.Settings.Default.WordWrap;
            UpdateControlsStates();
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(CurrentLangResourceManager.GetString("HelpPage"));
        }

        private void copyClipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //DataObject oldDataObject;
            CopyClipToClipboard(/*out oldDataObject*/);
            if (Properties.Settings.Default.MoveCopiedClipToTop)
                GetClipboardData();
        }

        private void toolStripButtonSelectTopClipOnShow_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.SelectTopClipOnShow = !Properties.Settings.Default.SelectTopClipOnShow;
            UpdateControlsStates();
        }
        private void UpdateControlsStates()
        {
            selectTopClipOnShowToolStripMenuItem.Checked = Properties.Settings.Default.SelectTopClipOnShow;
            toolStripButtonSelectTopClipOnShow.Checked = Properties.Settings.Default.SelectTopClipOnShow;
            wordWrapToolStripMenuItem.Checked = Properties.Settings.Default.WordWrap;
            toolStripButtonWordWrap.Checked = Properties.Settings.Default.WordWrap;
            dataGridView.Columns["VisualWeight"].Visible = Properties.Settings.Default.ShowVisualWeightColumn;
            richTextBox.WordWrap = wordWrapToolStripMenuItem.Checked;
        }

        private void toolStripMenuItemClearFilterAndSelectTop_Click(object sender, EventArgs e)
        {
            ClearFilter(-1);
        }

        private void changeClipTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.CurrentRow != null)
            {
                string oldTitle = RowReader["Title"] as string;
                InputBoxResult inputResult = InputBox.Show(CurrentLangResourceManager.GetString("HowUseAutoTitle"), CurrentLangResourceManager.GetString("EditClipTitle"), oldTitle, this);
                if (inputResult.ReturnCode == DialogResult.OK)
                {
                    string sql = "Update Clips set Title=@Title where Id=@Id";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.Parameters.Add("@Id", DbType.Int32).Value = RowReader["Id"];
                    string newTitle;
                    if (inputResult.Text == "")
                        newTitle = TextClipTitle(RowReader["text"].ToString());
                    else
                        newTitle = inputResult.Text;
                    command.Parameters.Add("@Title", DbType.String).Value = newTitle;
                    command.ExecuteNonQuery();
                    UpdateClipBindingSource();
                }
            }
        }

        private void setFavouriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetRowMark("Favorite", true, true);
        }

        private void resetFavouriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetRowMark("Favorite", false, true);
        }

        private void MarkFilter_SelectedValueChanged(object sender, EventArgs e)
        {
            if (AllowFilterProcessing)
            {
                UpdateClipBindingSource(); 
            }
        }

        private void showAllMarksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MarkFilter.SelectedValue = "allMarks";
        }

        private void showOnlyUsedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MarkFilter.SelectedValue = "used";
        }

        private void showOnlyFavoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MarkFilter.SelectedValue = "favorite";
        }

        private void dataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (true
                && !IsKeyPassedFromFilterToGrid(e.KeyCode, e.Control)
                && e.KeyCode != Keys.Delete
                && e.KeyCode != Keys.Home
                && e.KeyCode != Keys.End
                && e.KeyCode != Keys.Enter
                && e.KeyCode != Keys.ShiftKey
                && e.KeyCode != Keys.Alt
                && e.KeyCode != Keys.Menu
                && e.KeyCode != Keys.Tab
                //&& e.KeyCode != Keys.F1
                //&& e.KeyCode != Keys.F2
                //&& e.KeyCode != Keys.F3
                //&& e.KeyCode != Keys.F4
                //&& e.KeyCode != Keys.F5
                //&& e.KeyCode != Keys.F6
                //&& e.KeyCode != Keys.F7
                //&& e.KeyCode != Keys.F8
                //&& e.KeyCode != Keys.F9
                //&& e.KeyCode != Keys.F10
                //&& e.KeyCode != Keys.F11
                //&& e.KeyCode != Keys.F12
                //&& !e.Alt
                )
            {
                comboBoxFilter.Focus();
                sendKey(comboBoxFilter.Handle, e.KeyData, false, true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                // Tired of trying to make it with TAB order
                richTextBox.Focus(); 
            }

        }

        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            RunUpdate();
        }

        private void checkUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckUpdate(true);
        }

        private void timerCheckUpdate_Tick(object sender, EventArgs e)
        {
            CheckUpdate();
        }

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RowShift(-1);
        }

        private void RowShift(int indexShift =-1)
        {
            if (dataGridView.CurrentRow == null)
                return;
            int currentRowIndex = dataGridView.CurrentRow.Index;
            if (false
                || indexShift < 0 && currentRowIndex == 0
                || indexShift > 0 && currentRowIndex == dataGridView.RowCount
               )
                return;
            DataRow nearDataRow = ((DataRowView)clipBindingSource[currentRowIndex + indexShift]).Row;
            int newID = (int)nearDataRow["ID"];
            DataRow currentDataRow = ((DataRowView)clipBindingSource[currentRowIndex]).Row;
            int oldID = (int)currentDataRow["ID"];
            int tempID = LastId + 1;
            string sql = "Update Clips set Id=@NewId where Id=@Id";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.Parameters.Add("@Id", DbType.Int32).Value = newID;
            command.Parameters.Add("@NewID", DbType.Int32).Value = tempID;
            command.ExecuteNonQuery();
            command.Parameters.Add("@Id", DbType.Int32).Value = oldID;
            command.Parameters.Add("@NewID", DbType.Int32).Value = newID;
            command.ExecuteNonQuery();
            command.Parameters.Add("@Id", DbType.Int32).Value = tempID;
            command.Parameters.Add("@NewID", DbType.Int32).Value = oldID;
            command.ExecuteNonQuery();
            //SelectCurrentRow();
            clipBindingSource.Position = currentRowIndex + indexShift;
            UpdateClipBindingSource();
        }

        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RowShift(1);
        }

        private void historyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Process.Start(CurrentLangResourceManager.GetString("HistoryOfChanges")); // Returns 0. Why?
            Process.Start("https://sourceforge.net/p/clip-angel/blog");
        }

        private void toolStripMenuItemPasteChars_Click(object sender, EventArgs e)
        {
            SendPaste(PasteMethod.SendChars);
         }

        private void openInDefaultApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenInDefaultApplication();
        }

        private void OpenInDefaultApplication()
        {
            string type = RowReader["type"].ToString();
            //string TempFile = Path.GetTempFileName();
            string tempFile = Path.GetTempPath() + "Clip " + RowReader["id"] + " copy";
            bool deleteAfterOpen = false;
            if (IsTextType(type))
            {
                tempFile += ".txt";
                File.WriteAllText(tempFile, RowReader["text"].ToString(), Encoding.Default);
                deleteAfterOpen = true;
            }
            else if (type == "img")
            {
                tempFile += ".bmp";
                ImageControl.Image.Save(tempFile);
                deleteAfterOpen = true;
            }
            else if (type == "file")
            {
                string[] tokens = Regex.Split(RowReader["text"].ToString(), @"\r?\n|\r");
                tempFile = tokens[0];
                if (!File.Exists(tempFile))
                    tempFile = "";
            }
            if (tempFile != "")
            {
                try
                {
                    Process.Start(tempFile);
                    if (deleteAfterOpen)
                    {
                        Thread.Sleep(500); // to be almost sure that file has been opened
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void windowAlwaysOnTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            windowAlwaysOnTopToolStripMenuItem.Checked = this.TopMost;
            toolStripButtonTopMostWindow.Checked = this.TopMost;
        }

        private void editClipTextToolStripMenuItem_Click(object sender = null, EventArgs e = null)
        {
            string clipType = RowReader["type"].ToString();
            if (!IsTextType(clipType))
                return;
            int selectionStart = richTextBox.SelectionStart;
            int selectionLength = richTextBox.SelectionLength;
            bool newEditMode = !EditMode;
            allowRowLoad = false;
            if (!newEditMode)
                SaveClipText();
            else
            {
                if (clipType != "text")
                {
                    AddClip(null, null, "", "", "text", RowReader["text"].ToString());
                    GotoLastRow();
                }
            }
            UpdateClipBindingSource();
            allowRowLoad = true;
            EditMode = newEditMode;
            AfterRowLoad(-1, true, selectionStart, selectionLength);
            editClipTextToolStripMenuItem.Checked = EditMode;
            toolStripMenuItemEditClipText.Checked = EditMode;
            pasteENTERToolStripMenuItem.Enabled = !EditMode;
        }

        private void timerReconnect_Tick(object sender, EventArgs e)
        {
            ConnectClipboard();
        }

        private void dataGridView_MouseHover(object sender, EventArgs e)
        {
            //Point clientPos = dataGridView.PointToClient(Control.MousePosition);
            //DataGridView.HitTestInfo hitInfo = dataGridView.HitTest(clientPos.X, clientPos.Y);
            //if (hitInfo.Type == (DataGridViewHitTestType) DataGrid.HitTestType.Cell)
            //{
            //    if (hitInfo.ColumnIndex == dataGridView.Columns["VisualWeight"].Index)
            //    {
            //        DataGridViewCell hoverCell = dataGridView.Rows[hitInfo.RowIndex].Cells[hitInfo.ColumnIndex];
            //        hoverCell.ToolTipText = CurrentLangResourceManager.GetString("VisualWeightTooltip"); // No effect
            //    }
            //}
        }

        private void dataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            if (e.ColumnIndex == dataGridView.Columns["VisualWeight"].Index)
            {
                DataGridViewCell hoverCell = row.Cells[e.ColumnIndex];
                if (hoverCell.Value != null)
                    hoverCell.ToolTipText = CurrentLangResourceManager.GetString("VisualWeightTooltip"); // No effect
            }
        }

        private void timerApplyTextFiler_Tick(object sender, EventArgs e)
        {
            TextFilterApply();
            timerApplyTextFiler.Stop();
        }

        private void dataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dataGridView.CurrentCell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                //dataGridView.Rows[e.RowIndex].Selected = true;
                //dataGridView.Focus();
            }
        }

        private void dataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            if (row.Cells["Title"].Value == null)
            {
                PrepareRow(row);
                e.PaintCells(e.ClipBounds, DataGridViewPaintParts.All);
                e.Handled = true;
            }
        }
    }
}

public sealed class KeyboardHook : IDisposable
{
    // http://stackoverflow.com/questions/2450373/set-global-hotkeys-using-c-sharp

    // Registers a hot key with Windows.
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    // Unregisters the hot key with Windows.
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Represents the window that is used internally to get the messages.
    /// </summary>
    private sealed class Window : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        public Window()
        {
            // create the handle for the window.
            this.CreateHandle(new CreateParams());
        }

        /// <summary>
        /// Overridden to get the notifications.
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // check if we got a hot key pressed.
            if (m.Msg == WM_HOTKEY)
            {
                // get the keys.
                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                EnumModifierKeys modifier = (EnumModifierKeys)((int)m.LParam & 0xFFFF);

                // invoke the event to notify the parent.
                if (KeyPressed != null)
                    KeyPressed(this, new KeyPressedEventArgs(modifier, key));
            }
        }

        public event EventHandler<KeyPressedEventArgs> KeyPressed;

        #region IDisposable Members

        public void Dispose()
        {
            this.DestroyHandle();
        }

        #endregion
    }

    private Window _window = new Window();
    private int _currentId;

    public KeyboardHook()
    {
        // register the event of the inner native window.
        _window.KeyPressed += delegate (object sender, KeyPressedEventArgs args)
        {
            if (KeyPressed != null)
                KeyPressed(this, args);
        };
    }

    public void UnregisterHotKeys()
    {
        // unregister all the registered hot keys.
        for (int i = _currentId; i > 0; i--)
        {
            UnregisterHotKey(_window.Handle, i);
        }
    }

    /// <summary>
    /// Registers a hot key in the system.
    /// </summary>
    /// <param name="modifier">The modifiers that are associated with the hot key.</param>
    /// <param name="key">The key itself that is associated with the hot key.</param>
    public void RegisterHotKey(EnumModifierKeys modifier, Keys key)
    {
        // increment the counter.
        _currentId = _currentId + 1;

        // register the hot key.
        if (!RegisterHotKey(_window.Handle, _currentId, (uint)modifier, (uint)key))
        {
            string hotkeyTitle = HotkeyTitle(key, modifier);
            string errorText = "Couldn’t register the hot key " + hotkeyTitle;
            //throw new InvalidOperationException(ErrorText);
            MessageBox.Show(errorText, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static string HotkeyTitle(Keys key, EnumModifierKeys modifier)
    {
        string hotkeyTitle = "";
        if ((modifier & EnumModifierKeys.Win) != 0)
            hotkeyTitle += Keys.Control.ToString() + " + ";
        if ((modifier & EnumModifierKeys.Control) != 0)
            hotkeyTitle += Keys.Control.ToString() + " + ";
        if ((modifier & EnumModifierKeys.Alt) != 0)
            hotkeyTitle += Keys.Alt.ToString() + " + ";
        if ((modifier & EnumModifierKeys.Shift) != 0)
            hotkeyTitle += Keys.Shift.ToString() + " + ";
        hotkeyTitle += key.ToString();
        return hotkeyTitle;
    }

    /// <summary>
    /// A hot key has been pressed.
    /// </summary>
    public event EventHandler<KeyPressedEventArgs> KeyPressed;

    #region IDisposable Members

    public void Dispose()
    {
        UnregisterHotKeys();
        // dispose the inner native window.
        _window.Dispose();
    }

    #endregion
}

/// <summary>
/// Event Args for the event that is fired after the hot key has been pressed.
/// </summary>
public class KeyPressedEventArgs : EventArgs
{
    private EnumModifierKeys _modifier;
    private Keys _key;

    internal KeyPressedEventArgs(EnumModifierKeys modifier, Keys key)
    {
        _modifier = modifier;
        _key = key;
    }

    public EnumModifierKeys Modifier
    {
        get { return _modifier; }
    }

    public Keys Key
    {
        get { return _key; }
    }
}

/// <summary>
/// The enumeration of possible modifiers.
/// </summary>
[Flags]
public enum EnumModifierKeys : uint
{
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

// Solution for casesensitivity in SQLite http://www.cyberforum.ru/ado-net/thread1708878.html
namespace ASC.Data.SQLite
{

    /// <summary>
    /// Класс переопределяет функцию Lower() в SQLite, т.к. встроенная функция некорректно работает с символами > 128
    /// </summary>
    [SQLiteFunction(Name = "lower", Arguments = 1, FuncType = FunctionType.Scalar)]
    public class LowerFunction : SQLiteFunction
    {

        /// <summary>
        /// Вызов скалярной функции Lower().
        /// </summary>
        /// <param name="args">Параметры функции</param>
        /// <returns>Строка в нижнем регистре</returns>
        public override object Invoke(object[] args)
        {
            if (args.Length == 0 || args[0] == null) return null;
            return ((string)args[0]).ToLower();
        }
    }

    /// <summary>
    /// Класс переопределяет функцию Upper() в SQLite, т.к. встроенная функция некорректно работает с символами > 128
    /// </summary>
    [SQLiteFunction(Name = "upper", Arguments = 1, FuncType = FunctionType.Scalar)]
    public class UpperFunction : SQLiteFunction
    {

        /// <summary>
        /// Вызов скалярной функции Upper().
        /// </summary>
        /// <param name="args">Параметры функции</param>
        /// <returns>Строка в верхнем регистре</returns>
        public override object Invoke(object[] args)
        {
            if (args.Length == 0 || args[0] == null) return null;
            return ((string)args[0]).ToUpper();
        }
    }
}
