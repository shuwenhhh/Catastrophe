# Catastrophe 游戏：10 个可执行验证 Batch

先关闭正在打开此项目的 Unity Editor，然后在本目录运行单个 Batch：

```bash
./run_batch.sh 01
```

运行全部验证：

```bash
./run_all.sh
```

每个脚本以 `0` 表示通过、`1` 表示验证失败、`3` 表示 Unity Editor 仍占用工程。日志写入 `Validation/BatchLogs/`。

1. 工程、LivingRoom 场景及 Build Settings
2. 小猫 Prefab、渲染器、Animator 与关键骨骼
3. Idle、Walk、Push Right、Jump 动画片段
4. 小猫控制器与基础物理组件
5. 扩大后的房间、新地板及墙体
6. 沙发、餐桌、椅子和桌面物件的独立层级与分区
7. 地板、桌椅、沙发材质与贴图资源
8. 地板与全部家具的 Collider
9. GameBootstrap、主摄像机和第三人称相机
10. 完整集成回归（依次执行 Batch 01–09 的全部断言）
