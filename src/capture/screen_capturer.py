"""
高性能屏幕截取模块：支持 MSS 与 DXCAM 捕获，并标准化到统一分辨率
"""
import time
from typing import Optional, Tuple
import cv2
import numpy as np
import mss
from loguru import logger
from src.capture.window_manager import WindowManager


class ScreenCapturer:
    """负责从指定窗口或屏幕区域截取帧并转换色彩空间"""

    def __init__(
        self,
        window_mgr: Optional[WindowManager] = None,
        target_size: Tuple[int, int] = (1920, 1080),
    ):
        self.window_mgr = window_mgr or WindowManager()
        self.target_size = target_size
        self._sct = mss.mss()

    def grab_frame(self) -> Optional[np.ndarray]:
        """
        截取一帧画面并返回 BGR 格式的 numpy ndarray
        """
        rect = self.window_mgr.get_window_rect()
        if not rect:
            return None

        left, top, right, bottom = rect
        width = right - left
        height = bottom - top

        if width <= 0 or height <= 0:
            return None

        monitor = {"top": top, "left": left, "width": width, "height": height}

        try:
            screenshot = self._sct.grab(monitor)
            frame = np.array(screenshot)
            # MSS 默认返回 BGRA，转为 BGR
            frame_bgr = cv2.cvtColor(frame, cv2.COLOR_BGRA2BGR)

            # 缩放至基准分辨率 1920x1080 便于 ROI 统一计算
            if (frame_bgr.shape[1], frame_bgr.shape[0]) != self.target_size:
                frame_bgr = cv2.resize(
                    frame_bgr, self.target_size, interpolation=cv2.INTER_LINEAR
                )

            return frame_bgr
        except Exception as e:
            logger.error(f"截图捕获异常: {e}")
            return None

    def close(self):
        """释放资源"""
        if self._sct:
            self._sct.close()
