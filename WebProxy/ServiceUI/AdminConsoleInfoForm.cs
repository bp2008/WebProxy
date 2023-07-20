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
		public AdminConsoleInfoForm()
		{
			InitializeComponent();
			Init();
		}
		private void Init()
		{
			lblValidationError.Text = "";

			WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);

			string adminIp = string.IsNullOrEmpty(adminEntry.ipAddress) ? "Any IP" : adminEntry.ipAddress;
			string adminHost = adminExit.host;
			adminHost = adminHost?.Replace("*", "");
			if (string.IsNullOrEmpty(adminHost))
				adminHost = "localhost";

			if (adminEntry.httpPortValid())
			{
				string url = "http://" + adminHost + (adminEntry.httpPort == 80 ? "" : (":" + adminEntry.httpPort));
				linkLabelHttp.Text = url + " (" + adminIp + ")";
				linkLabelHttp.Tag = url;
				linkLabelHttp.LinkArea = new LinkArea(0, url.Length);
			}
			else
			{
				linkLabelHttp.Text = "Disabled";
				linkLabelHttp.Enabled = false;
			}
			if (adminEntry.httpsPortValid())
			{
				string url = "https://" + adminHost + (adminEntry.httpsPort == 443 ? "" : (":" + adminEntry.httpsPort));
				linkLabelHttps.Text = url + " (" + adminIp + ")";
				linkLabelHttps.Tag = url;
				linkLabelHttps.LinkArea = new LinkArea(0, url.Length);
			}
			else
			{
				linkLabelHttps.Text = "Disabled";
				linkLabelHttps.Enabled = false;
			}

			string user = adminLogin.AuthCredentials[0].User;
			string pass = adminLogin.AuthCredentials[0].Pass;
			txtUser.Text = user;
			txtUser.Tag = user;
			txtPass.Text = pass;
		}

		private void linkLabelHttp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start((string)((LinkLabel)sender).Tag);
		}

		private void linkLabelHttps_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start((string)((LinkLabel)sender).Tag);
		}

		private void txtUser_TextChanged(object sender, EventArgs e)
		{
			if (ValidateCredentials())
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				Settings s = WebProxyService.CloneSettingsObjectSlow();
				Middleware editableAdminLogin = s.middlewares.FirstOrDefault(m => m.Id == WebProxyService.AdminConsoleLoginId);
				if (editableAdminLogin == null)
					throw new ApplicationException("Unable to locate Admin Login Middleware to change credentials.");
				editableAdminLogin.SetPassword((string)txtUser.Tag, null);
				editableAdminLogin.SetPassword(txtUser.Text, txtPass.Text);
				WebProxyService.SaveNewSettings(s);
				txtUser.Tag = txtUser.Text;
				btnCopyUser.Enabled = btnCopyPass.Enabled = true;
			}
			else
				btnCopyUser.Enabled = btnCopyPass.Enabled = false;
		}

		private void txtPass_TextChanged(object sender, EventArgs e)
		{
			if (ValidateCredentials())
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				Settings s = WebProxyService.CloneSettingsObjectSlow();
				Middleware editableAdminLogin = s.middlewares.FirstOrDefault(m => m.Id == WebProxyService.AdminConsoleLoginId);
				if (editableAdminLogin == null)
					throw new ApplicationException("Unable to locate Admin Login Middleware to change credentials.");
				editableAdminLogin.SetPassword(txtUser.Text, txtPass.Text);
				WebProxyService.SaveNewSettings(s);
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
