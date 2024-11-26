# Onllama.MyRegistry
> Please ensure that you have already [installed the .NET SDK](https://learn.microsoft.com/en-us/dotnet/core/install/linux) for your environment.

> 请确保已经 [安装 .NET SDK](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux) 运行环境
- Server / 服务端
```
ollama pull library/qwen2.5

git clone https://github.com/mili-tan/Onllama.MyRegistry
cd Onllama.MyRegistry
dotnet run -c Release
```
- Client / 客户端
```
ollama pull <Server>/library/qwen2.5 --insecure
ollama cp <Server>/library/qwen2.5 library/qwen2.5
```
## TODO
- [ ] CLI
- [ ] HTTPS
- [ ] Ollama Push
- [ ] Identity / Ollama Keys
- [ ] Fallback to reverse proxy
