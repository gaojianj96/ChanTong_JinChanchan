"""
商店区域识别：识别当前商店 5 个卡槽的英雄名称、费用与刷新状态
"""
from typing import List, Optional, Dict, Any
import numpy as np
from loguru import logger
from src.vision.roi_manager import ROIManager
from src.vision.ocr_engine import OCREngine


class ShopDetector:
    """负责解析 5 格商店中的卡牌信息"""

    def __init__(self, roi_mgr: ROIManager, ocr_engine: OCREngine):
        self.roi_mgr = roi_mgr
        self.ocr_engine = ocr_engine
        self.last_shop_cards: List[Optional[str]] = [None] * 5

    def detect_shop(self, frame: np.ndarray) -> List[Optional[Dict[str, Any]]]:
        """
        识别当前商店 5 张卡牌
        返回格式: [{'slot': 0, 'name': '亚索', 'cost': 1}, ...]
        """
        shop_results = []

        for i in range(5):
            slot_key = f"shop_cards.slot_{i}"
            slot_img = self.roi_mgr.crop_roi(frame, slot_key)
            if slot_img is None:
                shop_results.append(None)
                continue

            card_name = self.ocr_engine.extract_text(slot_img)
            # 简单清洗与匹配
            cleaned_name = card_name.replace(" ", "") if card_name else None

            if cleaned_name:
                shop_results.append({
                    "slot": i,
                    "name": cleaned_name,
                    "raw_text": card_name
                })
            else:
                shop_results.append(None)

        return shop_results
