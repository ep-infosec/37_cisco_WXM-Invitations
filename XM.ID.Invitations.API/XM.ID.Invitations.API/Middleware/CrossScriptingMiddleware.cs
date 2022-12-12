using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace XM.ID.Invitations.API.Middleware
{
    public class CrossScriptingMiddleware
    {
        private readonly RequestDelegate _next;
        private ErrorMessage _error;
        private readonly int _statusCode = (int)HttpStatusCode.BadRequest;

        public CrossScriptingMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context)
        {
            // Check XSS in URL
            if (!string.IsNullOrWhiteSpace(context.Request.Path.Value))
            {
                var url = context.Request.Path.Value;

                if (CrossSiteScriptingValidation.IsStringDangerous(url, out _))
                {
                    await ErrorResponse(context).ConfigureAwait(false);
                    return;
                }
            }

            
            if (!string.IsNullOrWhiteSpace(context.Request.QueryString.Value))
            {
                var queryString = WebUtility.UrlDecode(context.Request.QueryString.Value);

                if (CrossSiteScriptingValidation.IsStringDangerous(queryString, out _))
                {
                    await ErrorResponse(context).ConfigureAwait(false);
                    return;
                }
            }

            
            var originalBody = context.Request.Body;
            try
            {
                var content = await ReadRequestBody(context);

                if (CrossSiteScriptingValidation.IsStringDangerous(content, out _))
                {
                    await ErrorResponse(context).ConfigureAwait(false);
                    return;
                }
                await _next(context).ConfigureAwait(false);
            }
            finally
            {
                context.Request.Body = originalBody;
            }
        }

        private static async Task<string> ReadRequestBody(HttpContext context)
        {
            var memorybuffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(memorybuffer);
            context.Request.Body = memorybuffer;
            memorybuffer.Position = 0;

            var content = await new StreamReader(memorybuffer, Encoding.UTF8).ReadToEndAsync();
            context.Request.Body.Position = 0;

            return content;
        }

        private async Task ErrorResponse(HttpContext context)
        {
            context.Response.Clear();
            context.Response.Headers.AddHeaders();
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = _statusCode;

            if (_error == null)
            {
                _error = new ErrorMessage
                {
                    Description = "Bad Request - Invalid characters found.",
                    ErrorCode = 400
                };
            }

            await context.Response.WriteAsync(_error.ToJSON());
        }
    }

    public static class CrossScriptingMiddlewareExt
    {
        public static IApplicationBuilder UseScriptingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CrossScriptingMiddleware>();
        }
    }

    public static class CrossSiteScriptingValidation
    {
        private static readonly char[] SpecialCharaters = { '<', '&' };

        public static bool IsStringDangerous(string s, out int matchIndex)
        {
            matchIndex = 0;

            for (var i = 0; ;)
            {
                var n = s.IndexOfAny(SpecialCharaters, i);

                if (n < 0) return false;

                if (n == s.Length - 1) return false;

                matchIndex = n;

                switch (s[n])
                {
                    case '<':
                        // if < is followed by !,/,? or any character then it may be html script
                        if (IsCharaterAtoZ(s[n + 1]) || s[n + 1] == '!' || s[n + 1] == '/' || s[n + 1] == '?') return true;
                        break;
                    case '&':
                        // If & is followed by # then also it may be html script
                        if (s[n + 1] == '#') return true;
                        break;

                }

                // Continue searching
                i = n + 1;
            }
        }

        private static bool IsCharaterAtoZ(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        public static void AddHeaders(this IHeaderDictionary headers)
        {
            if (headers["P3P"].IsNullOrEmpty())
            {
                headers.Add("P3P", "CP=\"IDC DSP COR ADM DEVi TAIi PSA PSD IVAi IVDi CONi HIS OUR IND CNT\"");
            }
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source == null || !source.Any();
        }
        public static string ToJSON(this object value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }

    public class ErrorMessage
    {
        public int ErrorCode { get; set; }
        public string Description { get; set; }
    }
}
