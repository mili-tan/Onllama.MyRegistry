﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Onllama.MyRegistry
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var modelpath = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
            Console.WriteLine(modelpath);

            try
            {
                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                    })
                    .ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(80,
                            listenOptions =>
                            {
                                //listenOptions.UseHttps();
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            });
                    })

                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            Console.WriteLine(context.Request.Path);
                            await next.Invoke();
                        });
                        app.Map("/v2", svr =>
                        {
                            app.UseRouting().UseEndpoints(endpoint =>
                            {
                                app.Use(async (context, next) =>
                                {
                                    Console.WriteLine(context.Request.Path);
                                    await next.Invoke();
                                });

                                endpoint.Map(
                                    "/v2/{rope}/{model}/blobs/{hash}",
                                    async context =>
                                    {
                                        foreach (var itemHeader in context.Request.Headers)
                                        {
                                            Console.WriteLine(itemHeader.Key + ":" + itemHeader.Value);
                                        }
                                        var hash = context.Request.RouteValues["hash"].ToString().Replace(':', '-');
                                        var path = Path.Combine(modelpath, "blobs", hash);
                                        if (File.Exists(path))
                                        {
                                            var fileInfo = new FileInfo(path);
                                            var fileLength = fileInfo.Length;

                                            // 处理Range请求
                                            //if (context.Request.Headers.ContainsKey("Range"))
                                            //{
                                            //    var rangeHeader = context.Request.Headers["Range"].ToString();
                                            //    var range = rangeHeader.Replace("bytes=", "").Split('-');
                                            //    var start = long.Parse(range[0]);
                                            //    var end = range.Length > 1 ? long.Parse(range[1]) : fileLength - 1;

                                            //    if (start >= fileLength || end >= fileLength)
                                            //    {
                                            //        context.Response.StatusCode = StatusCodes.Status416RequestedRangeNotSatisfiable;
                                            //        return;
                                            //    }

                                            //    context.Response.Headers.Location = context.Request.Path.ToString();
                                            //    context.Response.StatusCode = StatusCodes.Status206PartialContent;
                                            //    context.Response.ContentType = "application/octet-stream";
                                            //    context.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileLength}");
                                            //    context.Response.ContentLength = end - start + 1;

                                            //    using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                                            //    fileStream.Seek(start, SeekOrigin.Begin);
                                            //    await fileStream.CopyToAsync(context.Response.Body);
                                            //}
                                            //else
                                            {
                                                context.Response.Headers.Location = context.Request.Path.ToString();
                                                context.Response.ContentLength = fileLength;
                                                context.Response.StatusCode = StatusCodes.Status200OK;
                                                context.Response.ContentType = "application/octet-stream";
                                                context.Response.Headers.Add("Content-Disposition", $"attachment; filename={hash}");

                                                await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                                                await fileStream.CopyToAsync(context.Response.Body);
                                            }
                                        }
                                        else
                                            context.Response.StatusCode = 404;
                                    });

                                endpoint.Map(
                                    "/v2/{rope}/{model}/manifests/{tag}",
                                    async context =>
                                    {
                                        var rope = context.Request.RouteValues["rope"].ToString();
                                        var model = context.Request.RouteValues["model"].ToString();
                                        var tag = context.Request.RouteValues["tag"].ToString();
                                        var path = Path.Combine(modelpath, "manifests\\registry.ollama.ai", rope, model, tag);
                                        Console.WriteLine(path);
                                        if (File.Exists(path))
                                            await context.Response.SendFileAsync(path);
                                        else
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
        }
    }
}
