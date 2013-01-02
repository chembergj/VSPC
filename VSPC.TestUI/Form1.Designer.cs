namespace VSPC.TestUI
{
    partial class Form1
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
            this.buttonSimLogin = new System.Windows.Forms.Button();
            this.buttonSimLogoff = new System.Windows.Forms.Button();
            this.textBoxMessages = new System.Windows.Forms.TextBox();
            this.labelMsg = new System.Windows.Forms.Label();
            this.richTextBoxMessages = new System.Windows.Forms.RichTextBox();
            this.buttonFSDLogoff = new System.Windows.Forms.Button();
            this.buttonFSDLogin = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonSimLogin
            // 
            this.buttonSimLogin.Location = new System.Drawing.Point(12, 12);
            this.buttonSimLogin.Name = "buttonSimLogin";
            this.buttonSimLogin.Size = new System.Drawing.Size(109, 23);
            this.buttonSimLogin.TabIndex = 0;
            this.buttonSimLogin.Text = "Simconnect Login";
            this.buttonSimLogin.UseVisualStyleBackColor = true;
            this.buttonSimLogin.Click += new System.EventHandler(this.buttonSimLogin_Click);
            // 
            // buttonSimLogoff
            // 
            this.buttonSimLogoff.Location = new System.Drawing.Point(12, 41);
            this.buttonSimLogoff.Name = "buttonSimLogoff";
            this.buttonSimLogoff.Size = new System.Drawing.Size(109, 23);
            this.buttonSimLogoff.TabIndex = 1;
            this.buttonSimLogoff.Text = "Simconnect Logoff";
            this.buttonSimLogoff.UseVisualStyleBackColor = true;
            this.buttonSimLogoff.Click += new System.EventHandler(this.buttonSimLogoff_Click);
            // 
            // textBoxMessages
            // 
            this.textBoxMessages.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxMessages.Location = new System.Drawing.Point(12, 239);
            this.textBoxMessages.Multiline = true;
            this.textBoxMessages.Name = "textBoxMessages";
            this.textBoxMessages.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxMessages.Size = new System.Drawing.Size(706, 126);
            this.textBoxMessages.TabIndex = 2;
            // 
            // labelMsg
            // 
            this.labelMsg.AutoSize = true;
            this.labelMsg.Location = new System.Drawing.Point(12, 223);
            this.labelMsg.Name = "labelMsg";
            this.labelMsg.Size = new System.Drawing.Size(134, 13);
            this.labelMsg.TabIndex = 3;
            this.labelMsg.Text = "Messages and Exceptions:";
            // 
            // richTextBoxMessages
            // 
            this.richTextBoxMessages.Location = new System.Drawing.Point(15, 71);
            this.richTextBoxMessages.Name = "richTextBoxMessages";
            this.richTextBoxMessages.Size = new System.Drawing.Size(703, 149);
            this.richTextBoxMessages.TabIndex = 4;
            this.richTextBoxMessages.Text = "";
            // 
            // buttonFSDLogoff
            // 
            this.buttonFSDLogoff.Location = new System.Drawing.Point(127, 41);
            this.buttonFSDLogoff.Name = "buttonFSDLogoff";
            this.buttonFSDLogoff.Size = new System.Drawing.Size(109, 23);
            this.buttonFSDLogoff.TabIndex = 6;
            this.buttonFSDLogoff.Text = "FSD Logoff";
            this.buttonFSDLogoff.UseVisualStyleBackColor = true;
            this.buttonFSDLogoff.Click += new System.EventHandler(this.buttonFSDLogoff_Click);
            // 
            // buttonFSDLogin
            // 
            this.buttonFSDLogin.Location = new System.Drawing.Point(127, 12);
            this.buttonFSDLogin.Name = "buttonFSDLogin";
            this.buttonFSDLogin.Size = new System.Drawing.Size(109, 23);
            this.buttonFSDLogin.TabIndex = 5;
            this.buttonFSDLogin.Text = "FSD Login";
            this.buttonFSDLogin.UseVisualStyleBackColor = true;
            this.buttonFSDLogin.Click += new System.EventHandler(this.buttonFSDLogin_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(730, 377);
            this.Controls.Add(this.buttonFSDLogoff);
            this.Controls.Add(this.buttonFSDLogin);
            this.Controls.Add(this.richTextBoxMessages);
            this.Controls.Add(this.labelMsg);
            this.Controls.Add(this.textBoxMessages);
            this.Controls.Add(this.buttonSimLogoff);
            this.Controls.Add(this.buttonSimLogin);
            this.Name = "Form1";
            this.Text = "VSPC Test Application";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonSimLogin;
        private System.Windows.Forms.Button buttonSimLogoff;
        public System.Windows.Forms.TextBox textBoxMessages;
		private System.Windows.Forms.Label labelMsg;
		public System.Windows.Forms.RichTextBox richTextBoxMessages;
        private System.Windows.Forms.Button buttonFSDLogoff;
        private System.Windows.Forms.Button buttonFSDLogin;
    }
}

