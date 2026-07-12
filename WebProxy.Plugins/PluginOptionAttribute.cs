using System;

namespace WebProxy.Plugins
{
	/// <summary>
	/// <para>Optionally applied to public fields and public read/write properties of a plugin options class (see <see cref="WebProxyPlugin.OptionsType"/>) to control how the option is presented in the WebProxy Admin Console.</para>
	/// <para>Fields and properties of supported types are exposed as editable options even without this attribute.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class PluginOptionAttribute : Attribute
	{
		/// <summary>
		/// Human-friendly name shown as the option's label.  If omitted, the field/property name is shown.
		/// </summary>
		public string DisplayName { get; set; }
		/// <summary>
		/// Help text shown with the option when the user has help text enabled.
		/// </summary>
		public string HelpText { get; set; }
		/// <summary>
		/// Placeholder text shown in empty text inputs.
		/// </summary>
		public string Placeholder { get; set; }
		/// <summary>
		/// For string options: if true, a multi-line text area is shown instead of a single-line text input.
		/// </summary>
		public bool Multiline { get; set; }
		/// <summary>
		/// For numeric options: minimum allowed value.
		/// </summary>
		public double Min { get; set; } = double.NaN;
		/// <summary>
		/// For numeric options: maximum allowed value.
		/// </summary>
		public double Max { get; set; } = double.NaN;
	}
}
