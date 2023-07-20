
namespace WebProxy.ServiceUI
{
	partial class AdminConsoleInfoForm
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
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.linkLabelHttp = new System.Windows.Forms.LinkLabel();
			this.linkLabelHttps = new System.Windows.Forms.LinkLabel();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.txtUser = new System.Windows.Forms.TextBox();
			this.txtPass = new System.Windows.Forms.TextBox();
			this.lblValidationError = new System.Windows.Forms.Label();
			this.btnCopyUser = new System.Windows.Forms.Button();
			this.btnCopyPass = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label1.Location = new System.Drawing.Point(12, 149);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(154, 16);
			this.label1.TabIndex = 2;
			this.label1.Text = "Admin Console Bindings";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(22, 175);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(39, 13);
			this.label2.TabIndex = 3;
			this.label2.Text = "HTTP:";
			// 
			// linkLabelHttp
			// 
			this.linkLabelHttp.AutoSize = true;
			this.linkLabelHttp.Location = new System.Drawing.Point(32, 193);
			this.linkLabelHttp.Name = "linkLabelHttp";
			this.linkLabelHttp.Size = new System.Drawing.Size(55, 13);
			this.linkLabelHttp.TabIndex = 5;
			this.linkLabelHttp.TabStop = true;
			this.linkLabelHttp.Text = "linkLabel1";
			this.linkLabelHttp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelHttp_LinkClicked);
			// 
			// linkLabelHttps
			// 
			this.linkLabelHttps.AutoSize = true;
			this.linkLabelHttps.Location = new System.Drawing.Point(32, 229);
			this.linkLabelHttps.Name = "linkLabelHttps";
			this.linkLabelHttps.Size = new System.Drawing.Size(55, 13);
			this.linkLabelHttps.TabIndex = 6;
			this.linkLabelHttps.TabStop = true;
			this.linkLabelHttps.Text = "linkLabel2";
			this.linkLabelHttps.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelHttps_LinkClicked);
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(22, 211);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(46, 13);
			this.label3.TabIndex = 6;
			this.label3.Text = "HTTPS:";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label4.Location = new System.Drawing.Point(12, 9);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(206, 16);
			this.label4.TabIndex = 8;
			this.label4.Text = "Admin Console Login Credentials";
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(22, 35);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(40, 13);
			this.label5.TabIndex = 9;
			this.label5.Text = "USER:";
			// 
			// label8
			// 
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(22, 79);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(38, 13);
			this.label8.TabIndex = 11;
			this.label8.Text = "PASS:";
			// 
			// txtUser
			// 
			this.txtUser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtUser.Location = new System.Drawing.Point(25, 51);
			this.txtUser.Name = "txtUser";
			this.txtUser.Size = new System.Drawing.Size(245, 20);
			this.txtUser.TabIndex = 1;
			this.txtUser.TextChanged += new System.EventHandler(this.txtUser_TextChanged);
			// 
			// txtPass
			// 
			this.txtPass.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtPass.Location = new System.Drawing.Point(25, 95);
			this.txtPass.Name = "txtPass";
			this.txtPass.Size = new System.Drawing.Size(245, 20);
			this.txtPass.TabIndex = 3;
			this.txtPass.TextChanged += new System.EventHandler(this.txtPass_TextChanged);
			// 
			// lblValidationError
			// 
			this.lblValidationError.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblValidationError.ForeColor = System.Drawing.Color.Red;
			this.lblValidationError.Location = new System.Drawing.Point(0, 122);
			this.lblValidationError.Name = "lblValidationError";
			this.lblValidationError.Size = new System.Drawing.Size(324, 23);
			this.lblValidationError.TabIndex = 14;
			this.lblValidationError.Text = "[Validation Error Goes Here]";
			this.lblValidationError.TextAlign = System.Drawing.ContentAlignment.TopCenter;
			// 
			// btnCopyUser
			// 
			this.btnCopyUser.BackColor = System.Drawing.SystemColors.Control;
			this.btnCopyUser.FlatAppearance.BorderSize = 0;
			this.btnCopyUser.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnCopyUser.Image = global::WebProxy.Properties.Resources.baseline_content_copy_black_24dp;
			this.btnCopyUser.Location = new System.Drawing.Point(276, 43);
			this.btnCopyUser.Name = "btnCopyUser";
			this.btnCopyUser.Size = new System.Drawing.Size(36, 36);
			this.btnCopyUser.TabIndex = 2;
			this.btnCopyUser.UseVisualStyleBackColor = false;
			this.btnCopyUser.Click += new System.EventHandler(this.btnCopyUser_Click);
			// 
			// btnCopyPass
			// 
			this.btnCopyPass.BackColor = System.Drawing.SystemColors.Control;
			this.btnCopyPass.FlatAppearance.BorderSize = 0;
			this.btnCopyPass.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnCopyPass.Image = global::WebProxy.Properties.Resources.baseline_content_copy_black_24dp;
			this.btnCopyPass.Location = new System.Drawing.Point(276, 87);
			this.btnCopyPass.Name = "btnCopyPass";
			this.btnCopyPass.Size = new System.Drawing.Size(36, 36);
			this.btnCopyPass.TabIndex = 4;
			this.btnCopyPass.UseVisualStyleBackColor = false;
			this.btnCopyPass.Click += new System.EventHandler(this.btnCopyPass_Click);
			// 
			// AdminConsoleInfoForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(324, 257);
			this.Controls.Add(this.btnCopyPass);
			this.Controls.Add(this.btnCopyUser);
			this.Controls.Add(this.lblValidationError);
			this.Controls.Add(this.txtPass);
			this.Controls.Add(this.txtUser);
			this.Controls.Add(this.label8);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.linkLabelHttps);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.linkLabelHttp);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Name = "AdminConsoleInfoForm";
			this.Text = "AdminConsoleInfoForm";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.LinkLabel linkLabelHttp;
		private System.Windows.Forms.LinkLabel linkLabelHttps;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.TextBox txtUser;
		private System.Windows.Forms.TextBox txtPass;
		private System.Windows.Forms.Label lblValidationError;
		private System.Windows.Forms.Button btnCopyUser;
		private System.Windows.Forms.Button btnCopyPass;
	}
}