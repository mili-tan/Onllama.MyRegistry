using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using McMaster.Extensions.CommandLineUtils;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.ResponseCompression;

namespace Onllama.MyRegistry
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var hostname = string.Empty;
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var allowUA = new List<string> {"ollama", "go-http-client"};
            var cmd = new CommandLineApplication
            {
                Name = "Onllama.MyRegistry",
                Description = "Onllama.MyRegistry - Running your own Ollama Registry locally." +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License"
            };
            cmd.HelpOption("-?|-h|--help|-he");

            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var modelPathOption = cmd.Option<string>("-m|--model <path>",
                isZh ? "模型文件路径。" : "Set model path",
                CommandOptionType.SingleValue);
            var httpsOption = cmd.Option("-s|--https", isZh ? "启用 HTTPS。（默认自签名，不推荐）" : "Set enable HTTPS (Self-signed by default, not recommended)",
                CommandOptionType.NoValue);
            var uaOption = cmd.Option("-u", isZh ? "限制客户端 UA" : "Limit client User-Agent",
                CommandOptionType.NoValue);
            var pemOption = cmd.Option<string>("-pem|--pemfile <FilePath>",
                isZh ? "PEM 证书路径。 <./cert.pem>" : "Set your pem certificate file path <./cert.pem>",
                CommandOptionType.SingleOrNoValue);
            var keyOption = cmd.Option<string>("-key|--keyfile <FilePath>",
                isZh ? "PEM 证书密钥路径。 <./cert.key>" : "Set your pem certificate key file path <./cert.key>",
                CommandOptionType.SingleOrNoValue);
            var hostOption = cmd.Option<string>("-h|--host <Hostname>",
                isZh ? "设置允许的主机名。（默认全部允许）" : "Set the allowed host names. (Allow all by default)",
                CommandOptionType.SingleOrNoValue); var listen = new IPEndPoint(IPAddress.Any, 80);
            var modelPath = Environment.GetEnvironmentVariable("OLLAMA_MODELS") ??
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama",
            "models");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Directory.Exists("/usr/share/ollama/.ollama/models"))
                modelPath = "/usr/share/ollama/.ollama/models";

            cmd.OnExecute(() =>
            {
                if (httpsOption.HasValue()) listen.Port = 443;
                if (ipOption.HasValue()) listen = IPEndPoint.Parse(ipOption.Value());
                if (modelPathOption.HasValue()) modelPath = modelPathOption.Value();
                if (hostOption.HasValue()) hostname = hostOption.Value();
                Console.WriteLine("ModelPath:" + modelPath);

                try
                {
                    var host = new WebHostBuilder()
                        .UseKestrel()
                        .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
                            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);

                            services.AddResponseCompression(options =>
                            {
                                options.Providers.Add<BrotliCompressionProvider>();
                                options.Providers.Add<GzipCompressionProvider>();
                            });
                        })
                        .ConfigureKestrel(options =>
                        {
                            options.Listen(listen,
                                listenOptions =>
                                {
                                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                    if (httpsOption.HasValue()) listenOptions.UseHttps();
                                    if (pemOption.HasValue() && keyOption.HasValue())
                                        listenOptions.UseHttps(X509Certificate2.CreateFromPem(
                                            File.ReadAllText(pemOption.Value()), File.ReadAllText(keyOption.Value())));
                                });
                        })

                        .Configure(app =>
                        {
                            app.Map("/v2", svr =>
                            {
                                app.UseRouting().UseEndpoints(endpoint =>
                                {
                                    app.Use(async (context, next) =>
                                    {
                                        Console.WriteLine(context.Request.Path);
                                        await next.Invoke();
                                    });
                                    app.Use(async (context, next) =>
                                    {
                                        if (!string.IsNullOrWhiteSpace(hostname) &&
                                            !string.Equals(context.Request.Host.Host, hostname,
                                                StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            context.Response.StatusCode = 403;
                                            context.Response.ContentType = "text/plain";
                                            await context.Response.WriteAsync("Host Not Found");
                                            return;
                                        }

                                        if (uaOption.HasValue() && !allowUA.Any(x =>
                                                context.Request.Headers.UserAgent.ToString().ToLower().Contains(x)))
                                        {
                                            context.Response.StatusCode = 403;
                                            context.Response.ContentType = "text/plain";
                                            await context.Response.WriteAsync("Unsupported Client");
                                            return;
                                        }

                                        await next.Invoke();
                                    });

                                    endpoint.Map(
                                        "/v2/{rope}/{model}/blobs/{hash}",
                                        async context =>
                                        {
                                            Console.WriteLine("Blobs:" + context.Connection.RemoteIpAddress + "|" + string.Join(
                                                ' ', context.Request.Headers.Select(x => x.Key + ":" + x.Value)));

                                            var hash = context.Request.RouteValues["hash"].ToString().Replace(':', '-');
                                            var path = Path.Combine(modelPath, "blobs", hash);

                                            if (File.Exists(path))
                                            {
                                                var fileLength = new FileInfo(path).Length;
                                                context.Response.Headers.Location = context.Request.Path.ToString();
                                                context.Response.ContentType = "application/octet-stream";

                                                if (context.Request.Headers.ContainsKey("Range"))
                                                {
                                                    var rangeHeader = context.Request.Headers["Range"].ToString();
                                                    var range = rangeHeader.Replace("bytes=", "").Split('-');
                                                    var start = long.Parse(range[0]);
                                                    var end = range.Length > 1 ? long.Parse(range[1]) : fileLength - 1;
                                                    var length = end - start + 1;

                                                    if (start >= fileLength || end >= fileLength)
                                                    {
                                                        context.Response.StatusCode =
                                                            StatusCodes.Status416RequestedRangeNotSatisfiable;
                                                        return;
                                                    }

                                                    context.Response.ContentLength = length;
                                                    context.Response.StatusCode = StatusCodes.Status206PartialContent;
                                                    context.Response.Headers.Add("Content-Range",
                                                        $"bytes {start}-{end}/{fileLength}");

                                                    await context.Response.SendFileAsync(path, start, length);
                                                }
                                                else
                                                {
                                                    context.Response.ContentLength = fileLength;
                                                    context.Response.StatusCode = StatusCodes.Status200OK;
                                                    context.Response.Headers.Add("Content-Disposition",
                                                        $"attachment; filename={hash}");

                                                    await context.Response.SendFileAsync(path);
                                                }
                                            }
                                            else
                                                context.Response.StatusCode = 404;
                                        });

                                    endpoint.Map(
                                        "/v2/{rope}/{model}/manifests/{tag}",
                                        async context =>
                                        {
                                            Console.WriteLine("Manifests:" + context.Connection.RemoteIpAddress + "|" + string.Join(
                                                ' ', context.Request.Headers.Select(x => x.Key + ":" + x.Value)));

                                            var rope = context.Request.RouteValues["rope"].ToString();
                                            var model = context.Request.RouteValues["model"].ToString();
                                            var tag = context.Request.RouteValues["tag"].ToString();

                                            foreach (var path in Directory
                                                         .GetDirectories(Path.Combine(modelPath, "manifests")).ToList()
                                                         .Select(subs => Path.Combine(subs, rope, model, tag)))
                                            {
                                                if (!File.Exists(path)) continue;
                                                Console.WriteLine("Manifests:" + path);
                                                await context.Response.SendFileAsync(path);
                                                return;
                                            }

                                            context.Response.StatusCode = 404;
                                        });
                                });
                            });
                        }).Build();

                    host.Run();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            cmd.Execute(args);
        }
    }
}
