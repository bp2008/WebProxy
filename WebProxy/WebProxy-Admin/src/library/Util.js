/**
 * Splits the given string on ',' and ' ', removing empty entries.
 * @param {String} str String to split.
 */
export function splitExitpointHostList(str)
{
	return str.split(/,| /).filter(Boolean);
}
export function Clamp(i, min, max)
{
	if (i < min)
		return min;
	if (i > max)
		return max;
	if (isNaN(i))
		return min;
	return i;
}
var escape = document.createElement('textarea');
export function EscapeHTML(html)
{
	escape.textContent = html;
	return escape.innerHTML;
}
export function UnescapeHTML(html)
{
	escape.innerHTML = html;
	return escape.textContent;
}
export function HtmlAttributeEncode(str)
{
	if (typeof str !== "string")
		return "";
	var sb = new Array("");
	for (var i = 0; i < str.length; i++)
	{
		var c = str.charAt(i);
		switch (c)
		{
			case '"':
				sb.push("&quot;");
				break;
			case '\'':
				sb.push("&#39;");
				break;
			case '&':
				sb.push("&amp;");
				break;
			case '<':
				sb.push("&lt;");
				break;
			case '>':
				sb.push("&gt;");
				break;
			default:
				sb.push(c);
				break;
		}
	}
	return sb.join("");
}
var htmlToTextConvert = document.createElement('div');
/**
 * Given a string of HTML, returns the innerText representation.
 * @param {String} html HTML to get text out of
 */
export function HTMLToText(html)
{
	htmlToTextConvert.innerHTML = html;
	let text = htmlToTextConvert.innerText;
	htmlToTextConvert.innerHTML = "";
	return text;
}
/**
 * Escapes a minimal set of characters (\, *, +, ?, |, {, [, (,), ^, $, ., #, and white space) by replacing them with their escape codes. This instructs the regular expression engine to interpret these characters literally rather than as metacharacters.
 * @param {String} str String to escape.
 */
export function escapeRegExp(str)
{
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
