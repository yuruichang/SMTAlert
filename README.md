# SMTAlert - EVE Online Intel & Kill Feed Alert Tool

SMTAlert 是一个基于 [SMT (Slazanger's Eve Map Tool)](https://github.com/Slazanger/SMT) 二次开发的 EVE Online 预警工具。专注于星图预警可视化、击杀推送和角色位置追踪。

SMTAlert is a secondary development based on [SMT (Slazanger's Eve Map Tool)](https://github.com/Slazanger/SMT), focusing on star map intel visualization, kill feed alerts, and character location tracking.

## 功能 | Features

- **星图预警** — 实时读取 EVE 本地聊天日志，解析星系名称，在星图上高亮显示预警和清除状态
- **范围/星域视图** — 支持跳跃范围树形布局和星域实际布局两种地图模式，可缩放拖拽
- **击杀推送** — 接入 zKillboard RedisQ 实时击杀流，按星域过滤，支持 EN/CN 双语舰船名
- **ESI 集成** — OAuth PKCE 登录获取角色在线位置，自动跟随星域切换
- **悬浮窗** — 星图和击杀列表独立悬浮窗，支持透明度分离调节，始终置顶
- **多语言** — 支持中文 (zh-CN) 和英文 (en-US) 界面切换

## 系统要求 | Requirements

- Windows 10/11 (x64)
- .NET 8.0 Desktop Runtime
- EVE Online 本地聊天日志路径: `Documents\EVE\logs\Chatlogs`

## 编译 | Build

```bash
dotnet build SMTAlert/SMTAlert.csproj -c Release
```

### 单文件发布 | Single-file Publish

```bash
dotnet publish SMTAlert/SMTAlert.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## 原始项目 | Original Project

本项目基于 [SMT (Slazanger's Eve Map Tool)](https://github.com/Slazanger/SMT) 二次开发。

原始项目论坛: https://forums.eveonline.com/t/smt-eve-map-tool/3845/

Discord: https://discord.gg/QSuJjNn

## 开源许可 | License

基于原始 SMT 项目的 [MIT License](LICENSE) 开源。

Copyright (c) 2023 Slazanger
