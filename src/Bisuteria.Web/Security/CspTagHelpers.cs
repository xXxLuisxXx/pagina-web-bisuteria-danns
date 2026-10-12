using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bisuteria.Web.Security;

// El generador MVC agrega un li vacío con style="display:none" al resumen válido.
[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class CspValidationSummaryTagHelper : TagHelper
{
    public override int Order => int.MaxValue;
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        foreach (var content in new[] { output.Content, output.PreContent, output.PostContent })
            content.SetHtmlContent(content.GetContent().Replace("<li style=\"display:none\"></li>", "<li hidden></li>", StringComparison.Ordinal));
    }
}

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
