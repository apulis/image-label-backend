using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebUI.TagHelpers
{
    [HtmlTargetElement(Attributes = nameof(IsAdmin))]
    public class AuthTagHelper:TagHelper
    {
        public bool IsAdmin { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!IsAdmin)
            {
                output.SuppressOutput();
            }
        }
    }
}
