# Onllama.MyRegistry
> Please ensure that you have already [installed the .NET SDK](https://learn.microsoft.com/en-us/dotnet/core/install/linux) for your environment.

> 请确保已经 [安装 .NET SDK](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux) 运行环境
- Server / 服务端
```
#for Windows
#net stop http

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
- Usage / 使用
```
Onllama.MyRegistry - Running your own Ollama Registry locally.
Copyright (c) 2024 Milkey Tan. Code released under the MIT License

Usage: Onllama.MyRegistry [options]

Options:
  -?|-he|--help             Show help information / 查看帮助信息。
  -l|--listen <IPEndPoint>  Set server listening address and port / 监听的地址与端口。
  -m|--model <path>         Set model path / 模型文件路径。
  -h|--host <host>          Set model path / 设置允许的主机名。（默认全部允许）
  -s|--https                   Set enable HTTPS (Self-signed by default, not recommended) / 启用 HTTPS。（默认自签名，不推荐）
  -pem|--pemfile[:<FilePath>]  Set your pem certificate file path <./cert.pem> / PEM 证书路径。 <./cert.pem>
  -key|--keyfile[:<FilePath>]  Set your pem certificate key file path <./cert.key> / PEM 证书密钥路径。 <./cert.key>
```
## TODO
- [x] CLI
- [x] HTTPS
- [ ] Ollama Push
- [ ] Identity / Ollama Keys
- [ ] Fallback to reverse proxy
