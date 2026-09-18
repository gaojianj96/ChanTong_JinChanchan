# 阵容码总表(小鱼一图流 · 9.11-9.16 版本)

> 来源: 用户提供 ｜ 日期: 2026-09-16 ｜ 作者: 小鱼一图流(DataTFT)
> 用途: 这是"第三方测试完毕的资讯"的结构化来源, 含阵容名/阵容码/强度信号/适用条件。用于驱动资讯系统(MetaInfo)的数据结构设计与强度分级。

## 字段说明
- `阵容码`: 游戏内粘贴码, 格式 `#阵容名-作者#base64`
- `建议1`: 上手/强度信号(可硬玩=无脑、刚需=必须有特定海克斯/装备、≤1同行=怕抢牌、糊X=依赖抽到某子)
- `建议2`: 条件/版本状态(有转更强=有转职更强、升S+=特定条件升强度、热补已削=版本削弱)

## 阵容列表

| # | 阵容名 | 阵容码 | 链接 | 建议1 | 建议2 |
|---|---|---|---|---|---|
| 1 | 猎龙95 | #猎龙95-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzE0MjE1NjMx | https://mp.weixin.qq.com/s/S9ASGEe0gTK6VSuvH0lQcQ | 可硬玩 | 有转更强 |
| 2 | 5个3星韦鲁斯 | #5个3星韦鲁斯-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4MDg3ODg5MzM3 | https://mp.weixin.qq.com/s/APs2mD89rvlUFsbnyqe74A | ≤1同行 | 很吃对位 |
| 3 | 6D主宰女警 | #6D主宰女警-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5NTIyODUzMzMx | https://mp.weixin.qq.com/s/GnHLBhIKU7CSrY9fASU9eg | 可硬玩 | 猪/河蟹多、有战刃升S+ |
| 4 | 6主宰Flex | #6主宰Flex-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzA3MTQ4ODM5 | https://mp.weixin.qq.com/s/IU3HLSXWZ9LPCTNFwp2dzQ | 硬玩吃分 | 主宰转S+ |
| 5 | 40层女警 | #40层女警-小鱼一图流#MjE5OTE0MDkxODI4MDc2MDkxNzg3Mzc0OTg0NTk2 | https://mp.weixin.qq.com/s/L5CY4r3uFyxuc3MZXR8cRg | 可硬玩 | 蜘蛛多、有战刃升S+ |
| 6 | 裁决螳螂 | #裁决螳螂-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MDQ5MzEzNzU2 | https://mp.weixin.qq.com/s/qKSbqBsWF7LPlneOQPAq2w | 1-2螳螂 | 螳螂装备 |
| 7 | 花妖84 | #花妖84-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4Mzg3NTE5NTY3 | https://mp.weixin.qq.com/s/nIVaYrnCpeWdz8wmrGn0tg | 可硬玩 | 有转更强、有裁决转/丽花升S+ |
| 8 | 7野小红小蓝 | #7野小红小蓝-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4NTIyNTI5OTE0 | https://mp.weixin.qq.com/s/T8j1Y5g_K_5vhpkXOi1QQw | 糊红/蓝 | 2-5前5野 |
| 9 | 拼盘天梯 | #拼盘天梯-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4NjUyNDU5NjIy | https://mp.weixin.qq.com/s/EjI21goL_oglQcMBOaUR5g | 刚需 | 拼盘天梯 |
| 10 | 5迅射月男(需转) | #5迅射月男(需转)-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzE2NzY2NjE0 | https://mp.weixin.qq.com/s/EWfYMyfUYsdmioMhoG5-cQ | 刚需 | 迅射转、热补已削 |
| 11 | 大龙95Flex | #大龙95Flex-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MjcwMjAzMDAz | https://mp.weixin.qq.com/s/BFSXajAOxL-l6iDSgadxlw | 刚需 | 大经济 |
| 12 | 月男Flex | #月男Flex-小鱼和木木尼#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4MDQ3NzQ3NzYw | https://mp.weixin.qq.com/s/U-BJsCHx0RdKqp_3AlCeMw | 可硬玩 | 收250更强 |

## 强度信号归纳(从"建议1/2"列提炼)

| 信号词 | 语义 | 建议映射 |
|---|---|---|
| 可硬玩 / 硬玩吃分 | 无脑可玩, 下限高 | 强度高, 上手易 |
| 刚需 | 必须有特定海克斯/转职/装备才能玩 | 条件型阵容, 需特定前置 |
| ≤1同行 / 很吃对位 | 怕抢牌/依赖站位克制 | 环境敏感 |
| 有转更强 / 主宰转S+ / 升S+ | 有转职时强度跃升 | 条件升级 |
| 热补已削 | 版本削弱 | 已过时/降级 |
| 糊红/蓝 / 1-2螳螂 | 依赖抽到特定棋子 | 依赖开局牌型 |
