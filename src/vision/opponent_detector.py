"""
对手侦察识别模块：识别当前是否处于切屏观战对手状态，并记录各家阵容与备战席情况
"""
from typing import Dict, Any, Optional
import numpy as np
from loguru import logger
from src.vision.roi_manager import ROIManager
from src.vision.ocr_engine import OCREngine


class OpponentDetector:
    """对手状态追踪与侦察缓存"""

    def __init__(self, roi_mgr: ROIManager, ocr_engine: OCREngine):
        self.roi_mgr = roi_mgr
        self.ocr_engine = ocr_engine
        # 记录 7 个对手的历史快照
        self.opponents_history: Dict[str, Dict[str, Any]] = {}

    def is_viewing_opponent(self, frame: np.ndarray) -> bool:
        """
        判断当前画面是否切在对手棋盘（通常顶部会有对手昵称或者返回自身棋盘按钮）
        """
        # 依据屏幕特定特征判断
        return False

    def update_opponent_snapshot(
        self,
        opponent_name: str,
        board_units: list,
        bench_units: list,
        traits: list
    ):
        """更新某位对手的阵容与备战席快照"""
        self.opponents_history[opponent_name] = {
            "board_units": board_units,
            "bench_units": bench_units,
            "traits": traits
        }
        logger.info(f"已更新对手 [{opponent_name}] 的阵容快照")
