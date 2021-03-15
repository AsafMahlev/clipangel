﻿namespace ClipAngel
{
    partial class ConnectRecipientForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConnectRecipientForm));
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.QuantityOfRecipients = new System.Windows.Forms.Label();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxChannelName = new System.Windows.Forms.TextBox();
            this.buttonReset = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxRecipients = new System.Windows.Forms.TextBox();
            this.checkBoxShow = new System.Windows.Forms.CheckBox();
            this.textBoxSenderName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.toolTip2 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox
            // 
            resources.ApplyResources(this.pictureBox, "pictureBox");
            this.pictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.TabStop = false;
            this.toolTip2.SetToolTip(this.pictureBox, resources.GetString("pictureBox.ToolTip"));
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            this.toolTip2.SetToolTip(this.label1, resources.GetString("label1.ToolTip"));
            // 
            // QuantityOfRecipients
            // 
            resources.ApplyResources(this.QuantityOfRecipients, "QuantityOfRecipients");
            this.QuantityOfRecipients.Name = "QuantityOfRecipients";
            this.toolTip2.SetToolTip(this.QuantityOfRecipients, resources.GetString("QuantityOfRecipients.ToolTip"));
            // 
            // buttonRefresh
            // 
            resources.ApplyResources(this.buttonRefresh, "buttonRefresh");
            this.buttonRefresh.Name = "buttonRefresh";
            this.toolTip2.SetToolTip(this.buttonRefresh, resources.GetString("buttonRefresh.ToolTip"));
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            this.toolTip2.SetToolTip(this.label2, resources.GetString("label2.ToolTip"));
            // 
            // textBoxChannelName
            // 
            resources.ApplyResources(this.textBoxChannelName, "textBoxChannelName");
            this.textBoxChannelName.Name = "textBoxChannelName";
            this.textBoxChannelName.ReadOnly = true;
            this.toolTip2.SetToolTip(this.textBoxChannelName, resources.GetString("textBoxChannelName.ToolTip"));
            // 
            // buttonReset
            // 
            resources.ApplyResources(this.buttonReset, "buttonReset");
            this.buttonReset.Name = "buttonReset";
            this.toolTip2.SetToolTip(this.buttonReset, resources.GetString("buttonReset.ToolTip"));
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label3.Name = "label3";
            this.toolTip2.SetToolTip(this.label3, resources.GetString("label3.ToolTip"));
            // 
            // textBoxRecipients
            // 
            resources.ApplyResources(this.textBoxRecipients, "textBoxRecipients");
            this.textBoxRecipients.Name = "textBoxRecipients";
            this.textBoxRecipients.ReadOnly = true;
            this.toolTip2.SetToolTip(this.textBoxRecipients, resources.GetString("textBoxRecipients.ToolTip"));
            // 
            // checkBoxShow
            // 
            resources.ApplyResources(this.checkBoxShow, "checkBoxShow");
            this.checkBoxShow.Name = "checkBoxShow";
            this.toolTip2.SetToolTip(this.checkBoxShow, resources.GetString("checkBoxShow.ToolTip"));
            this.checkBoxShow.UseVisualStyleBackColor = true;
            this.checkBoxShow.CheckedChanged += new System.EventHandler(this.checkBoxShow_CheckedChanged);
            // 
            // textBoxSenderName
            // 
            resources.ApplyResources(this.textBoxSenderName, "textBoxSenderName");
            this.textBoxSenderName.Name = "textBoxSenderName";
            this.textBoxSenderName.ReadOnly = true;
            this.toolTip2.SetToolTip(this.textBoxSenderName, resources.GetString("textBoxSenderName.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            this.toolTip2.SetToolTip(this.label4, resources.GetString("label4.ToolTip"));
            // 
            // ConnectRecipientForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textBoxSenderName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.checkBoxShow);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxRecipients);
            this.Controls.Add(this.textBoxChannelName);
            this.Controls.Add(this.buttonReset);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.QuantityOfRecipients);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureBox);
            this.KeyPreview = true;
            this.Name = "ConnectRecipientForm";
            this.toolTip2.SetToolTip(this, resources.GetString("$this.ToolTip"));
            this.Load += new System.EventHandler(this.ConnectRecipientForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ConnectRecipientForm_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label QuantityOfRecipients;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxChannelName;
        private System.Windows.Forms.Button buttonReset;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxRecipients;
        private System.Windows.Forms.CheckBox checkBoxShow;
        private System.Windows.Forms.TextBox textBoxSenderName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ToolTip toolTip2;
    }
}