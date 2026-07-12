using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WebProxy.Plugins
{
	/// <summary>
	/// Describes one editable option of a plugin options class, in a form suitable for dynamically generating an editor UI.
	/// </summary>
	public class PluginOptionField
	{
		/// <summary>
		/// Name of the field or property in the options class.  This is also the key used when persisting the option's value.
		/// </summary>
		public string Key;
		/// <summary>
		/// Human-friendly name to show as the option's label.
		/// </summary>
		public string DisplayName;
		/// <summary>
		/// Help text shown with the option, or null.
		/// </summary>
		public string HelpText;
		/// <summary>
		/// Placeholder text for empty text inputs, or null.
		/// </summary>
		public string Placeholder;
		/// <summary>
		/// One of: "string", "multiline", "bool", "number", "enum", "stringArray".
		/// </summary>
		public string FieldType;
		/// <summary>
		/// For FieldType "number": true if the underlying type is an integer type.
		/// </summary>
		public bool IsInteger;
		/// <summary>
		/// For FieldType "enum": the list of allowed values.
		/// </summary>
		public string[] EnumValues;
		/// <summary>
		/// The option's default value (from a default-constructed options object).  Enums are represented by name.
		/// </summary>
		public object DefaultValue;
		/// <summary>
		/// For FieldType "number": minimum allowed value, or null.
		/// </summary>
		public double? Min;
		/// <summary>
		/// For FieldType "number": maximum allowed value, or null.
		/// </summary>
		public double? Max;
	}
	/// <summary>
	/// Builds <see cref="PluginOptionField"/> schemas from plugin options classes via reflection.
	/// </summary>
	public static class PluginOptionSchema
	{
		private static readonly HashSet<Type> integerTypes = new HashSet<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong) };
		private static readonly HashSet<Type> floatTypes = new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) };
		/// <summary>
		/// <para>Builds the option field schema for the given plugin options type.  Returns an empty list if the type is null.</para>
		/// <para>Public instance fields and public instance read/write properties are included, in declaration order.  Unsupported member types are silently omitted.</para>
		/// </summary>
		/// <param name="optionsType">Type of a plugin options class, having a public parameterless constructor.  May be null.</param>
		/// <returns>List of option fields.</returns>
		public static List<PluginOptionField> BuildSchema(Type optionsType)
		{
			List<PluginOptionField> schema = new List<PluginOptionField>();
			if (optionsType == null)
				return schema;

			object defaultsInstance = Activator.CreateInstance(optionsType);

			IEnumerable<MemberInfo> members = optionsType
				.GetMembers(BindingFlags.Public | BindingFlags.Instance)
				.Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property)
				.OrderBy(m => m.MetadataToken);

			foreach (MemberInfo member in members)
			{
				Type memberType;
				object defaultValue;
				if (member is FieldInfo fi)
				{
					if (fi.IsInitOnly || fi.IsLiteral)
						continue;
					memberType = fi.FieldType;
					defaultValue = fi.GetValue(defaultsInstance);
				}
				else if (member is PropertyInfo pi)
				{
					if (!pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length > 0 || pi.GetSetMethod() == null)
						continue;
					memberType = pi.PropertyType;
					defaultValue = pi.GetValue(defaultsInstance);
				}
				else
					continue;

				PluginOptionAttribute attr = member.GetCustomAttribute<PluginOptionAttribute>();

				PluginOptionField field = new PluginOptionField();
				field.Key = member.Name;
				field.DisplayName = string.IsNullOrWhiteSpace(attr?.DisplayName) ? member.Name : attr.DisplayName;
				field.HelpText = attr?.HelpText;
				field.Placeholder = attr?.Placeholder;
				field.DefaultValue = defaultValue;

				if (memberType == typeof(string))
					field.FieldType = attr?.Multiline == true ? "multiline" : "string";
				else if (memberType == typeof(bool))
					field.FieldType = "bool";
				else if (integerTypes.Contains(memberType) || floatTypes.Contains(memberType))
				{
					field.FieldType = "number";
					field.IsInteger = integerTypes.Contains(memberType);
					if (attr != null && !double.IsNaN(attr.Min))
						field.Min = attr.Min;
					if (attr != null && !double.IsNaN(attr.Max))
						field.Max = attr.Max;
				}
				else if (memberType.IsEnum)
				{
					field.FieldType = "enum";
					field.EnumValues = Enum.GetNames(memberType);
					field.DefaultValue = defaultValue?.ToString();
				}
				else if (memberType == typeof(string[]) || memberType == typeof(List<string>))
					field.FieldType = "stringArray";
				else
					continue; // Unsupported member type.

				schema.Add(field);
			}
			return schema;
		}
	}
}
