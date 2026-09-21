#!/usr/bin/env py
# -*- coding: utf-8 -*-
"""META-SEED 生成器: 把内嵌的静态阵容清单落成 MetaInfo comps.json / version.json。

- 输入: 本文件内嵌的 COMPS 权威静态清单(手工从 docs/meta/comp_code_table.md 转写, 不解析 markdown)。
- 输出: data/meta/S18/comps.json + data/meta/S18/version.json。
- 用法: py tools/meta_seed/generate.py (在仓库根目录执行)。
"""

import json
import os
import sys

PATCH = "S18"
PROVIDER = "小鱼一图流"

# 梯度映射: S+ -> T0, S -> T1, S- -> T2
TIER_MAP = {"S+": "T0", "S": "T1", "S-": "T2"}


def source(url):
    return {
        "type": "ThirdParty",
        "provider": PROVIDER,
        "patch": PATCH,
        "url": url,
        "confidence": 0.9,
    }


def req(two_star=(), emblems=(), items=(), augments=(), min_units=()):
    return {
        "requiredTwoStarUnits": [
            {"unitName": name, "minCount": count} for name, count in two_star
        ],
        "requiredEmblems": list(emblems),
        "requiredItems": list(items),
        "requiredAugments": list(augments),
        "minUnitCounts": [
            {"unitName": name, "minCount": count} for name, count in min_units
        ],
    }


def shift(condition, tier, effect):
    return {"condition": condition, "shiftedTier": tier, "effect": effect}


# 每条: (slug, name, comp_code, url, tier, prerequisites, conditionals, tier_note, settle_note)
# prerequisites -> req(...)；conditionals -> [(condition, shifted_tier, effect)]。
COMPS = [
    # ---- S+ ----
    ("diyu-huo-lunzi", "地狱火轮子",
     "#地狱火轮子-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5Nzk3NzM3OTUy",
     "https://mp.weixin.qq.com/s/azSZx2TEDLLlmITi98HHRA", "S+",
     req(emblems=["地狱火转"]),
     [], "如修还是有问题", None),
    ("qi-ye-flex", "7野Flex",
     "#7野Flex-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5NzAzMTM4MDQ5",
     "https://mp.weixin.qq.com/s/jMPxYs_CdGEf_kDK5kybdw", "S+",
     req(two_star=[("小蓝", 2)]),
     [shift("天使/帽子专", "T0", "升S+")], None, "2-5前5野"),
    ("lie-long-95", "猎龙95",
     "#猎龙95-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzE0MjE1NjMx",
     "https://mp.weixin.qq.com/s/S9ASGEe0gTK6VSuvH0lQcQ", "S+",
     req(),
     [], "有转更强", "可硬玩"),
    ("heian-yishi-zhizhu", "黑暗仪式蜘蛛",
     "#黑暗仪式蜘蛛-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5OTA2MjQ1MTc4",
     None, "S+",
     req(augments=["黑暗仪式"]),
     [], None, None),
    # ---- S ----
    ("shenyu-dazui-flex", "神谕大嘴Flex",
     "#神谕大嘴-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5Nzc1NDE1NjUy",
     "https://mp.weixin.qq.com/s/nWx9u2h4fMaKGSvMvZnauQ", "S",
     req(items=["大剑"], min_units=[("大嘴", 1)]),
     [shift("有战刃/神谕转", "T0", "升S+")], None, None),
    ("6d-zhuzai-nvjing", "6D主宰女警",
     "#6D主宰女警-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5NTIyODUzMzMx",
     "https://mp.weixin.qq.com/s/GnHLBhIKU7CSrY9fASU9eg", "S",
     req(),
     [shift("有战刃", "T0", "升S+")], None, "可硬玩、猪/河蟹多"),
    ("caijue-tanglang", "裁决螳螂",
     "#裁决螳螂-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5OTMwMTYxMTE3",
     "https://mp.weixin.qq.com/s/4A0eiWAVdMo3z3a0bh0juw", "S",
     req(min_units=[("螳螂", 1)]),
     [], "有优化", "螳螂装备"),
    ("5-xunshe-yuenan", "5迅射月男",
     "#5迅射月男(需转)-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzE2NzY2NjE0",
     "https://mp.weixin.qq.com/s/EWfYMyfUYsdmioMhoG5-cQ", "S",
     req(emblems=["迅射转"]),
     [], "热补已削", None),
    ("zhongzhuang-yuenan-flex", "重装月男Flex",
     "#月男Flex-小鱼和木木尼#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4MDQ3NzQ3NzYw",
     "https://mp.weixin.qq.com/s/U-BJsCHx0RdKqp_3AlCeMw", "S",
     req(),
     [], "收250更强", "可硬玩"),
    ("6-zhuzai-flex", "6主宰Flex",
     "#6主宰Flex-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MzA3MTQ4ODM5",
     "https://mp.weixin.qq.com/s/vd2tdyM2Ldf8N_Pi0xNtFQ", "S",
     req(),
     [shift("主宰转", "T0", "升S+")], None, "硬玩吃分"),
    ("huayao-jiela-84", "花妖婕拉84",
     "#花妖84-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4Mzg3NTE5NTY3",
     "https://mp.weixin.qq.com/s/nIVaYrnCpeWdz8wmrGn0tg", "S",
     req(),
     [shift("有裁决转/丽花", "T0", "升S+")], "有转更强", "可硬玩"),
    ("dalong-95-flex", "大龙95Flex",
     "#大龙95Flex-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MjcwMjAzMDAz",
     "https://mp.weixin.qq.com/s/BFSXajAOxL-l6iDSgadxlw", "S",
     req(augments=["大经济"]),
     [], "热补已削", None),
    ("5ge-3xing-yaoji", "5个3星妖姬",
     "#5个3妖姬-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5OTgzNzYwODY5",
     None, "S",
     req(two_star=[("奥恩", 2)], items=["青龙刀"]),
     [], "热补加强", None),
    ("pinpan-tianti", "拼盘天梯",
     "#拼盘天梯-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4NjUyNDU5NjIy",
     "https://mp.weixin.qq.com/s/EjI21goL_oglQcMBOaUR5g", "S",
     req(augments=["拼盘天梯"]),
     [], None, None),
    ("zhongzhuang-nvjing", "重装女警",
     "#40层女警-小鱼一图流#MjE5OTE0MDkxODI4MDc2MDkxNzg3Mzc0OTg0NTk2",
     "https://mp.weixin.qq.com/s/L5CY4r3uFyxuc3MZXR8cRg", "S",
     req(),
     [shift("有战刃", "T0", "升S+")], None, "可硬玩、蜘蛛多"),
    # ---- S- ----
    ("senlin-tianshi", "森林天使",
     "#森林天使-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg3MTc5ODkzNzAy",
     "https://mp.weixin.qq.com/s/yZLPmcItRIEm7xiLOAAhGg", "S-",
     req(two_star=[("奥恩", 2)], items=["羊刀"]),
     [], "热补加强", None),
    ("xianji-liu-shahuang", "献祭流沙皇",
     "#献祭流沙皇-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5OTI1OTMwMTYz",
     None, "S-",
     req(),
     [], "新增献祭螳螂流沙皇(详见附录)",
     "螳螂多、3莲开局。玩法(附录): 思路来源B站小古云顶之弈; 3莲华+螳螂早来+散件多大棒; "
     "献祭2/3星螳螂(进化裁决); 7级追3沙皇/蔚/螳螂; 沙皇装备: 3星螳螂→科技枪+法爆, 螳螂不3→羊刀。"),
    ("gunala-95", "古纳拉95",
     "#古纳拉95-小鱼X桃子别烦我#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MDc5MDE1Njc3",
     "https://mp.weixin.qq.com/s/a-j6Gsrc2zguFa9ZnAWyyA", "S-",
     req(augments=["大经济"]),
     [], None, None),
    ("senlin-yuenan", "森林月男",
     "#森林月男-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg3OTA5Mjk1Njc2",
     "https://mp.weixin.qq.com/s/03CdP_2UutdektyQ3c05Uw", "S-",
     req(emblems=["森林转"]),
     [], None, None),
    ("baobao-kamier", "宝宝卡蜜尔",
     "#51卡蜜尔-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5MjIxMjEyNDE3",
     "https://mp.weixin.qq.com/s/7EhcywCnMtu8gvsnNz4iqw", "S-",
     req(augments=["宝宝", "不侦查"], items=["神器"]),
     [shift("宝宝/暗爪+胡牌", "T1", "升S")], None, None),
    ("tese-jiansheng", "特色剑圣",
     "#特色剑圣-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg3MjAzNTAwOTUw",
     "https://mp.weixin.qq.com/s/Z7A9WMx_fm8b5YUZGyP3Ow", "S-",
     req(),
     [], None, None),
    ("5ge-3xing-weilusi", "5个3星韦鲁斯",
     "#5个3星韦鲁斯-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4MDg3ODg5MzM3",
     "https://mp.weixin.qq.com/s/APs2mD89rvlUFsbnyqe74A", "S-",
     req(),
     [], "热补变傻", "≤1同行、很吃对位"),
    ("dengdai-tianhu-xiaolan", "等待/天胡小蓝",
     "#等待小蓝-小鱼一图流#MjE5OTE0MDkxODI4MDc2MDkxNzg3NDA4NDkwOTI5",
     None, "S-",
     req(two_star=[("小蓝", 2)]),
     [], "详见附录",
     "刚需开局2星小蓝+装备偏水滴大棒。附录: 有等待得值则2阶段内D; 妖姬提莫2选1或挂3野怪; "
     "双复制器直接捏3; 升人口找5神谕; 无等待得值则2阶不D等3-1抽3; 神谕转给拉露恩换提莫。"),
    ("yeguai-shenyu-ali", "野怪神谕阿狸",
     "#野怪神谕阿狸-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg5NTU1MDg1NjE1",
     "https://mp.weixin.qq.com/s/ZzN3ZXtKU7UV-50TFCQ_Gw", "S-",
     req(),
     [], "热补加强", None),
    ("yeshou-ali", "野兽阿狸",
     "#5莲阿狸小偷+Flex-小鱼一图流#MjE5OTE0MDkxODI4MDc2MDkxNzg3MzI5NTA0NjU2",
     "https://mp.weixin.qq.com/s/cL4ahjjWJ3hHkV4DOhhb_A", "S-",
     req(),
     [], "热补加强", "奔着偷3星4费玩"),
    ("zhuzai-lie", "主宰猎",
     "#主宰猎人-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4NjA1MjY1MTk2",
     "https://mp.weixin.qq.com/s/8DWoYboJneKspSSP4GLuig", "S-",
     req(items=["青龙刀", "战刃"]),
     [], "火转=低配火艾希", None),
    ("yeguai-shitou-lie", "野怪石头猎",
     "#野怪石头猎-思路来源听雨#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4NDg2NjAwMjEz",
     "https://mp.weixin.qq.com/s/GGCyBr_MNdB1_gfuOTtcvw", "S-",
     req(items=["青龙刀", "战刃"]),
     [], None, None),
    ("bihuan-yeshou-shuangc", "闭环野兽双C",
     "#闭环野兽双C-小鱼一图流#MTA5OTYyOTQ3OTcyMzgyNTIxNzg4MzIxMzgwNTU0",
     "https://mp.weixin.qq.com/s/3RbTjCL8f_ADYdd0697nrg", "S-",
     req(items=["青龙刀", "战刃"]),
     [], None, None),
]


def build_comps():
    comps = []
    seen = set()
    for slug, name, code, url, tier, prerequisites, conditionals, tier_note, settle_note in COMPS:
        if slug in seen:
            raise SystemExit(f"重复 slug: {slug}")
        seen.add(slug)
        comp = {
            "id": slug,
            "name": name,
            "compCode": code,
            "intrinsicRequirements": prerequisites,
            "source": source(url),
        }
        if settle_note:
            comp["settleNote"] = settle_note
        comps.append(comp)
    return comps


def build_version():
    tiers = []
    for slug, _name, _code, _url, tier, _prereq, conditionals, tier_note, _settle in COMPS:
        entry = {
            "compId": slug,
            "tier": TIER_MAP[tier],
            "conditionals": conditionals,
            "source": source(None),
        }
        if tier_note:
            entry["note"] = tier_note
        tiers.append(entry)
    version = {
        "patch": PATCH,
        "source": source(None),
        "tiers": tiers,
    }
    return version


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out_dir = os.path.join(root, "data", "meta", PATCH)
    os.makedirs(out_dir, exist_ok=True)

    with open(os.path.join(out_dir, "comps.json"), "w", encoding="utf-8") as f:
        json.dump(build_comps(), f, ensure_ascii=False, indent=2)
        f.write("\n")

    with open(os.path.join(out_dir, "version.json"), "w", encoding="utf-8") as f:
        json.dump(build_version(), f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"wrote {len(COMPS)} comps + version to {out_dir}")


if __name__ == "__main__":
    main()