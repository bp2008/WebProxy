using BPUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebProxy.ServiceUI
{
	public partial class AdminConsoleInfoForm : Form
	{
		private bool Loaded = false;
		public AdminConsoleInfoForm()
		{
			InitializeComponent();
			Init();
		}
		private void Init()
		{
			lblValidationError.Text = "";

			AdminInfo adminInfo = new AdminInfo();

			if (adminInfo.httpUrl != null)
			{
				linkLabelHttp.Text = adminInfo.httpUrl + " (" + adminInfo.adminIp + ")";
				linkLabelHttp.Tag = adminInfo.httpUrl;
				linkLabelHttp.LinkArea = new LinkArea(0, adminInfo.httpUrl.Length);
			}
			else
			{
				linkLabelHttp.Text = "Disabled";
				linkLabelHttp.Enabled = false;
			}
			if (adminInfo.httpsUrl != null)
			{
				linkLabelHttps.Text = adminInfo.httpsUrl + " (" + adminInfo.adminIp + ")";
				linkLabelHttps.Tag = adminInfo.httpsUrl;
				linkLabelHttps.LinkArea = new LinkArea(0, adminInfo.httpsUrl.Length);
			}
			else
			{
				linkLabelHttps.Text = "Disabled";
				linkLabelHttps.Enabled = false;
			}

			txtUser.Text = adminInfo.user;
			txtUser.Tag = adminInfo.user;
			txtPass.Text = adminInfo.pass;
		}

		private void AdminConsoleInfoForm_Load(object sender, EventArgs e)
		{
			Loaded = true;
		}

		private void linkLabelHttp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			ProcessRunner.Start((string)((LinkLabel)sender).Tag);
		}

		private void linkLabelHttps_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			ProcessRunner.Start((string)((LinkLabel)sender).Tag);
		}

		private void txtUser_TextChanged(object sender, EventArgs e)
		{
			if (!Loaded)
				return;
			if (ValidateCredentials())
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				Settings s = WebProxyService.CloneSettingsObjectSlow();
				Middleware editableAdminLogin = s.middlewares.FirstOrDefault(m => m.Id == WebProxyService.AdminConsoleLoginId);
				if (editableAdminLogin == null)
					throw new ApplicationException("Unable to locate Admin Login Middleware to change credentials.");
				editableAdminLogin.SetPassword((string)txtUser.Tag, null);
				editableAdminLogin.SetPassword(txtUser.Text, txtPass.Text);
				TaskHelper.RunAsyncCodeSafely(() => WebProxyService.SaveNewSettings(s));
				txtUser.Tag = txtUser.Text;
				btnCopyUser.Enabled = btnCopyPass.Enabled = true;
			}
			else
				btnCopyUser.Enabled = btnCopyPass.Enabled = false;
		}

		private void txtPass_TextChanged(object sender, EventArgs e)
		{
			if (!Loaded)
				return;
			if (ValidateCredentials())
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				Settings s = WebProxyService.CloneSettingsObjectSlow();
				Middleware editableAdminLogin = s.middlewares.FirstOrDefault(m => m.Id == WebProxyService.AdminConsoleLoginId);
				if (editableAdminLogin == null)
					throw new ApplicationException("Unable to locate Admin Login Middleware to change credentials.");
				editableAdminLogin.SetPassword(txtUser.Text, txtPass.Text);
				TaskHelper.RunAsyncCodeSafely(() => WebProxyService.SaveNewSettings(s));
				btnCopyUser.Enabled = btnCopyPass.Enabled = true;
			}
			else
				btnCopyUser.Enabled = btnCopyPass.Enabled = false;
		}

		private bool ValidateCredentials()
		{
			if (txtUser.Text.Length < 3)
			{
				lblValidationError.Text = "USER must be >= 3 chars";
				return false;
			}
			else if (txtPass.Text.Length < 8)
			{
				lblValidationError.Text = "PASS must be >= 8 chars";
				return false;
			}
			else
			{
				lblValidationError.Text = "";
				return true;
			}
		}

		private void btnCopyUser_Click(object sender, EventArgs e)
		{
			CopyText(txtUser.Text);
			btnCopyUser.BackColor = Color.FromArgb(128, 255, 128);
			BPUtil.SetTimeout.OnGui(() =>
			{
				btnCopyUser.BackColor = SystemColors.Control;
			}, 500, this);
		}

		private void btnCopyPass_Click(object sender, EventArgs e)
		{
			CopyText(txtPass.Text);
			btnCopyPass.BackColor = Color.FromArgb(128, 255, 128);
			BPUtil.SetTimeout.OnGui(() =>
			{
				btnCopyPass.BackColor = SystemColors.Control;
			}, 500, this);
		}
		private void CopyText(string text)
		{
			Thread thread = new Thread(() => Clipboard.SetText(text));
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
		}
	}
}
