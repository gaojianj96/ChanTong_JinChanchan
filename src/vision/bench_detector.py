"""
备战席检测模块：识别己方及侦察时对手备战席上的 9 个槽位弈子与星级
"""
from typing import List, Optional, Dict, Any
import numpy as np
from loguru import logger
from src.vision.roi_manager import ROIManager
from src.vision.ocr_engine import OCREngine


class BenchDetector:
    """管理 9 格备战席的弈子识别"""

    def __init__(self, roi_mgr: ROIManager, ocr_engine: OCREngine):
        self.roi_mgr = roi_mgr
        self.ocr_engine = ocr_engine

    def detect_bench(self, frame: np.ndarray, is_opponent: bool = False) -> List[Optional[Dict[str, Any]]]:
        """
        检测 9 个备战席槽位
        """
        bench_results = []
        prefix = "opponent_bench" if is_opponent else "player_bench"

        for i in range(9):
            slot_key = f"{prefix}.slot_{i}"
            slot_img = self.roi_mgr.crop_roi(frame, slot_key)
            if slot_img is None:
                bench_results.append(None)
                continue

            # 提取名称/特征 (实际落地时可结合 YOLO 目标检测或模板特征比对)
            name = self.ocr_engine.extract_text(slot_img)
            if name:
                bench_results.append({
                    "slot": i,
                    "name": name,
                    "star": 1  # 默认1星，后续由星级检测器强化
                })
            else:
                bench_results.append(None)

        return bench_results
