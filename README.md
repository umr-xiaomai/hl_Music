# KugouMusicApi.NET

基于 .NET 10.0 开发的跨平台桌面音乐播放器，集成酷狗音乐 API

## 项目结构

```
KugouMusicApi.NET/
├── KuGou.Net/           # 酷狗 API 核心库
├── SimpleAudio/         # 音频播放组件
├── TestMusic/           # 桌面客户端
├── KgWebApi.Net/        # Web API 服务
└── ConsoleApp1/         # 控制台测试程序
```

## 界面预览

<!-- 播放器主界面 -->
![播放器主界面](docs/images/player-main.png)

<!-- 每日推荐 -->
![每日推荐](docs/images/每日推荐.png)

## 快速开始

### 环境要求

- .NET 10.0 SDK
- Windows/Linux/macOS

### 构建运行

```bash
# 克隆项目
git clone https://github.com/your-repo/KugouMusicApi.NET.git
cd KugouMusicApi.NET

# 构建项目
dotnet build KugouMusic.NET.slnx

# 运行桌面客户端
dotnet run --project TestMusic/TestMusic.csproj

# 运行 Web API
dotnet run --project KgWebApi.Net/KgWebApi.Net.csproj
```

