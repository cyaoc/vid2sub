# vid2sub
视频或音频转字幕。

在本项目的Release页面提供了windows x64下整合包，开箱即用。

# 语音转录
[Whisper.cpp](https://github.com/ggerganov/whisper.cpp)

Whisper.cpp是OpenAI开源项目Whisper的CPP实现，可以在没有显卡的PC上以较快的速度实现语音转文字。也是本项目的的核心。

非整合包用户，请按照Whisper.cpp的说明进行编译以及模型的转换，并将编译的exe放入根目录下的bin\cpu 或者 bin\openvino 目录，模型文件放入根目录下的models目录，当前模型只支持medium和large-v3。


# 视频转音频
[FFmpeg](https://github.com/FFmpeg/FFmpeg)

可以将视频中的音频提取出来，也可以将字幕压入视频，一个很好用的开源工具。

整合包中已经提供了对应的exe程序，不想使用整合包的，需要按照FFmpeg的说明，在系统中安装FFmpeg，并保证可以通过命令行直接使用。

# AI加速

本项目针对没有英伟达独立显卡的公司电脑而开发，对Intel的CPU提供加速服务。

非整合包用户需自行安装OpenVino环境，当前项目编译使用[2024.0.0](https://github.com/openvinotoolkit/openvino/releases/tag/2024.0.0)

# Powershell环境设定

[PowerShell运行脚本时出现“禁止运行脚本”时，按照下面方案解决](https://www.jianshu.com/p/2afbe757105c)
