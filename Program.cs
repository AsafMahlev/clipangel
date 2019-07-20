﻿//Copyright 2017 Starykh Sergey (tormozit) tormozit@gmail.com
//GNU General Public License version 3.0 (GPLv3)

using System;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Resources;
using Microsoft.Win32.SafeHandles;
using System.Security.Permissions;

namespace ClipAngel
{
    static class Program
    {
        static readonly string MainMutexName = "ClipAngelApplicationMutex";
        static Mutex MyMutex;
        static private int parentPID;
        static ResourceManager CurrentLangResourceManager;

        [STAThread]
        static void Main(string[] args)
        {
            bool elevatedMode = args.Contains("/elevated");
            if (elevatedMode)
            {
                parentPID = Int32.Parse(args[1]);
                string elevatedMutexName = "ClipAngelElevatedMutex" + parentPID;
                MyMutex = new Mutex(true, elevatedMutexName);
                if (!MyMutex.WaitOne(0, false))
                    return;
                Process parentProcess;
                try
                {
                    parentProcess = Process.GetProcessById(parentPID);
                }
                catch
                {
                    return;
                }
                EventWaitHandle pasteEventWaiter = Paster.GetPasteEventWaiter(parentPID);
                EventWaitHandle sendCharsEventWaiter = Paster.GetSendCharsEventWaiter(parentPID);

                // https://social.msdn.microsoft.com/Forums/vstudio/en-US/ade69140-490f-4070-b8b5-08ac80d1c557/how-to-waitany-on-a-process-a-manualresetevent?forum=netfxbcl
                ManualResetEvent processExitWaiter = new ManualResetEvent(true);
                processExitWaiter.SafeWaitHandle = new SafeWaitHandle(parentProcess.Handle, false);

                WaitHandle[] waitHandles = new WaitHandle[3] { pasteEventWaiter, sendCharsEventWaiter, processExitWaiter};
                while (true)
                {
                    try
                    {
                        Process.GetProcessById(parentPID);
                    }
                    catch
                    {
                        break;
                    }
                    int eventIndex = WaitHandle.WaitAny(waitHandles, 60000); // sometimes waitAny begins consume 100% cpu core. Set timeout is workaround try.
                    if (eventIndex != WaitHandle.WaitTimeout)
                    {
                        if (eventIndex == 0)
                        {
                            Paster.SendPaste();
                            pasteEventWaiter.Reset();
                        } else if (eventIndex == 1)
                        {
                            Paster.SendChars();
                            sendCharsEventWaiter.Reset();
                        }
                        else if (eventIndex == 2)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break; // sometimes waitAny begins consume 100% cpu core. This is workaround try.
                    }
                }
            }
            else
            {
                if (IsSingleInstance(MainMutexName))
                {
                    string UserSettingsPath = "";
                    bool PortableMode = false;
                    PortableMode = args.Contains("/p") || args.Contains("/portable");
                    // http://stackoverflow.com/questions/1382617/how-to-make-designer-generated-net-application-settings-portable/2579399#2579399
                    UserSettingsPath = MakePortable(Properties.Settings.Default, PortableMode);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Main Main = new Main(UserSettingsPath, PortableMode, args.Contains("/m"));
                    Application.AddMessageFilter(new TestMessageFilter());
                    Application.Run(Main);
                    Properties.Settings.Default.Save();
                }
            }
        }

        private static string MakePortable(ApplicationSettingsBase settings, bool PortableMode = true)
        {
            var portableSettingsProvider =
                new PortableSettingsProvider(settings.GetType().Name + ".settings", PortableMode);
            settings.Providers.Add(portableSettingsProvider);
            foreach (SettingsProperty prop in settings.Properties)
                prop.Provider = portableSettingsProvider;
            settings.Reload();
            return portableSettingsProvider.GetAppSettingsPath();
        }
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("User32.dll")]
        static extern IntPtr GetForegroundWindow();

        static bool IsSingleInstance(string MutexName)
        {
            MyMutex = new Mutex(true, MutexName);
            if (MyMutex.WaitOne(0, false))
                return true;
            Process[] allProcess = Process.GetProcesses();
            Process currentProcess = Process.GetCurrentProcess();
            foreach (Process process in allProcess)
            {
                if (currentProcess.ProcessName == process.ProcessName && currentProcess.Id != process.Id)
                {
                    IntPtr hWnd = GetProcessMainWindow(process.Id);
                    if (hWnd != IntPtr.Zero)
                    {
                        string dummy;
                        CurrentLangResourceManager = ClipAngel.Main.getResourceManager(out dummy);
                        ShowWindow(hWnd, 1);
                        SetForegroundWindow(hWnd);
                        MessageBox.Show(CurrentLangResourceManager.GetString("ActivatedExistedProcess"), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information, 
                            MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    }
                }
            }
            return false;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr window, out int process);

        [DllImport("user32.dll")]
        private static extern IntPtr GetProp(IntPtr hWnd, string lpString);

        static private IntPtr GetProcessMainWindow(int process)
        {
            IntPtr pLast = IntPtr.Zero;
            do
            {
                pLast = FindWindowEx(IntPtr.Zero, pLast, null, null);
                int iProcess_;
                GetWindowThreadProcessId(pLast, out iProcess_);
                if (iProcess_ == process)
                {
                    IntPtr propVal = GetProp(pLast, ClipAngel.Main.IsMainPropName);
                    if (propVal.ToInt32() == 1)
                        break;
                }
            } while (pLast != IntPtr.Zero);
            return pLast;
        }

        // To support exit by taskkill signal http://qaru.site/questions/1648501/windows-application-handle-taskkill
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        private class TestMessageFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == /*WM_CLOSE*/ 0x10)
                {
                    Application.Exit();
                    return true;
                }
                return false;
            }
        }
    }
}
