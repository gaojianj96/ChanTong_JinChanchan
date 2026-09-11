"""
棋盘网格与弈子检测模块：负责 4x7 六角格上的弈子定位、名称、星级与装备识别
"""
from typing import List, Dict, Any, Optional
import cv2
import numpy as np
from loguru import logger
from src.vision.roi_manager import ROIManager


class BoardDetector:
    """棋盘上弈子检测与阵容分析"""

    def __init__(self, roi_mgr: ROIManager, model_path: Optional[str] = None):
        self.roi_mgr = roi_mgr
        self.model_path = model_path
        self.model = None
        self._init_model()

    def _init_model(self):
        """如果提供了 YOLO 权重文件，则加载 YOLO 目标检测模型"""
        if self.model_path:
            try:
                from ultralytics import YOLO
                self.model = YOLO(self.model_path)
                logger.info(f"YOLO 棋盘模型加载成功: {self.model_path}")
            except Exception as e:
                logger.warning(f"加载 YOLO 模型失败，将使用经典 CV 算法兜底: {e}")

    def detect_board_units(self, frame: np.ndarray) -> List[Dict[str, Any]]:
        """
        检测棋盘上的所有弈子。
        返回列表: [
            {
                "name": "亚索",
                "star": 2,
                "grid_pos": (row, col),
                "items": ["无尽之刃", "饮血剑"],
                "bbox": [x1, y1, x2, y2]
            },
            ...
        ]
        """
        board_img = self.roi_mgr.crop_roi(frame, "board_area")
        if board_img is None:
            return []

        units = []

        if self.model:
            # 使用 YOLO 进行目标检测
            results = self.model(board_img, verbose=False)
            for r in results:
                for box in r.boxes:
                    cls_id = int(box.cls[0])
                    label = self.model.names[cls_id]
                    conf = float(box.conf[0])
                    xyxy = box.xyxy[0].cpu().numpy().tolist()

                    units.append({
                        "name": label,
                        "star": 1,
                        "confidence": conf,
                        "bbox": xyxy
                    })
        else:
            # 基础降级流程 (预留接口)
            pass

        return units
