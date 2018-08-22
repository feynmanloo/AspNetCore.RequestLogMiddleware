using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AspNetCore.RequestLogMiddleware
{
    public class RequestLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestLogMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RequestLogMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;
                var sp = new Stopwatch();
                sp.Start();
                await _next(context);
                sp.Stop();
                WriteLog(context.Request.Method, 
                    context.Request.Scheme, 
                    context.Request.Host.ToString(), 
                    context.Request.Path, 
                    context.Request.QueryString.ToString(), 
                    context.Connection.RemoteIpAddress.ToString(), 
                    sp.Elapsed.TotalMilliseconds, 
                    await FormatRequest(context.Request), 
                    await FormatResponse(context.Response));
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            request.EnableRewind();
            request.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            if (response.HasStarted)
            {
                return string.Empty;
            }
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }

        protected virtual void WriteLog(string method, string scheme, string host, string path, string query, string ip, double timeConsuming, string reqBody, string resBody)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}][{method}]  {scheme}://{host}{path}{query}");
            sb.AppendLine($"IP: {ip}");
            sb.AppendLine($"TimeConsuming: {timeConsuming}ms");
            sb.AppendLine($"Request: {reqBody}");
            sb.AppendLine($"Response: {resBody}");
            _logger.LogInformation(sb.ToString());
        }
    }

    public static class RequestLogExtensions
    {
        public static IApplicationBuilder UseRequestLog(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLogMiddleware>();
        }
    }
}
