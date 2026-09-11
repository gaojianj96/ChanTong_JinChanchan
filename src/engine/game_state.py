"""
游戏状态数据模型：表示当前局内的完整上下文
"""
from typing import List, Dict, Optional, Any
from pydantic import BaseModel, Field


class Unit(BaseModel):
    """棋子/弈子数据结构"""
    name: str
    cost: int = 1
    star: int = 1
    items: List[str] = Field(default_factory=list)
    grid_pos: Optional[Tuple_Pos] = None if False else None


class ShopItem(BaseModel):
    """商店单张卡牌"""
    slot: int
    name: str
    cost: int
    is_locked: bool = False


class OpponentSnapshot(BaseModel):
    """对手状态快照"""
    name: str
    hp: int = 100
    level: int = 1
    board_units: List[Unit] = Field(default_factory=list)
    bench_units: List[Unit] = Field(default_factory=list)
    traits: Dict[str, int] = Field(default_factory=dict)


class GameState(BaseModel):
    """局内完整实时状态"""
    stage: str = "1-1"
    gold: int = 0
    level: int = 1
    hp: int = 100
    exp: int = 0
    exp_to_next_level: int = 2

    # 己方棋盘与备战席
    board_units: List[Unit] = Field(default_factory=list)
    bench_units: List[Unit] = Field(default_factory=list)

    # 商店与卡池
    current_shop: List[Optional[ShopItem]] = Field(default_factory=list)
    shop_history: List[List[str]] = Field(default_factory=list)

    # 羁绊激活状态
    active_traits: Dict[str, int] = Field(default_factory=dict)

    # 所有对手状态 (最多7名对手)
    opponents: Dict[str, OpponentSnapshot] = Field(default_factory=dict)
