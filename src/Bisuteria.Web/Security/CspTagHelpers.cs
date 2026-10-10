using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bisuteria.Web.Security;

[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public sealed class CspNonceTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = default!;
    public override void Process(TagHelperContext context, TagHelperOutput output)
        => output.Attributes.SetAttribute("nonce", ViewContext.HttpContext.Items["CspNonce"]?.ToString());
}

// Convierte estilos del marcado Razor en reglas autorizadas por nonce; no permite style-src-attr.
[HtmlTargetElement("*", Attributes = "style")]
public sealed class CspStyleTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = default!;
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var style = output.Attributes["style"].Value.ToString() ?? "";
        output.Attributes.RemoveAll("style");
        if (style.Contains('<') || style.Contains('>') || style.Contains('{') || style.Contains('}')) return;
        var className = "csp-" + Guid.NewGuid().ToString("N");
        output.Attributes.SetAttribute("class", $"{output.Attributes["class"]?.Value} {className}".Trim());
        var nonce = ViewContext.HttpContext.Items["CspNonce"]?.ToString();
        output.PostElement.AppendHtml($"<style nonce=\"{nonce}\">.{className}{{{style}}}</style>");
    }
}
