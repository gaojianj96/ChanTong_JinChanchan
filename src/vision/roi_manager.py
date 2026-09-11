"""
ROI (Region of Interest) 区域截取与坐标管理
"""
from typing import Dict, Any, Tuple, Optional
import yaml
import numpy as np
from loguru import logger


class ROIManager:
    """管理并裁剪画面中各功能区块的图像"""

    def __init__(self, roi_config_path: str = "config/rois.yaml"):
        self.roi_config_path = roi_config_path
        self.config: Dict[str, Any] = {}
        self.load_config()

    def load_config(self):
        """加载 ROI 坐标配置"""
        try:
            with open(self.roi_config_path, "r", encoding="utf-8") as f:
                self.config = yaml.safe_load(f)
            logger.info("ROI 配置加载成功")
        except Exception as e:
            logger.error(f"加载 ROI 配置文件失败: {e}")
            self.config = {"regions": {}}

    def crop_roi(self, frame: np.ndarray, region_key: str) -> Optional[np.ndarray]:
        """
        根据配置名称裁剪图像区域
        region_key 可以是 'player_gold' 或 'shop_cards.slot_0'
        """
        if frame is None:
            return None

        keys = region_key.split(".")
        current = self.config.get("regions", {})
        for k in keys:
            if isinstance(current, dict) and k in current:
                current = current[k]
            else:
                logger.warning(f"未找到 ROI 键值: {region_key}")
                return None

        if isinstance(current, list) and len(current) == 4:
            x1, y1, x2, y2 = current
            h, w = frame.shape[:2]
            x1, y1 = max(0, x1), max(0, y1)
            x2, y2 = min(w, x2), min(h, y2)
            return frame[y1:y2, x1:x2]

        return None
