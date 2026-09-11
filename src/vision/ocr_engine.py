"""
OCR 文字与数字识别引擎：解析金币、阶段、等级、生命值及卡牌名称
"""
from typing import Optional, List
import re
import cv2
import numpy as np
from loguru import logger

try:
    import easyocr
    EASYOCR_AVAILABLE = True
except ImportError:
    EASYOCR_AVAILABLE = False


class OCREngine:
    """封装 OCR 引擎，提供文本与数字提取能力"""

    def __init__(self, languages: Optional[List[str]] = None, use_gpu: bool = True):
        self.languages = languages or ["ch_sim", "en"]
        self.reader = None
        if EASYOCR_AVAILABLE:
            try:
                self.reader = easyocr.Reader(self.languages, gpu=use_gpu)
                logger.info("EasyOCR 引擎初始化成功")
            except Exception as e:
                logger.warning(f"EasyOCR 初始化失败，降级为模拟/OpenCV识别: {e}")
        else:
            logger.warning("未安装 easyocr，OCR 功能将以规则/占位模式运行")

    def preprocess_for_digits(self, image: np.ndarray) -> np.ndarray:
        """对数字区域进行二值化与对比度增强以提高识别率"""
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        # 放大并提升对比度
        resized = cv2.resize(gray, (0, 0), fx=2.0, fy=2.0, interpolation=cv2.INTER_CUBIC)
        _, binary = cv2.threshold(resized, 180, 255, cv2.THRESH_BINARY)
        return binary

    def extract_text(self, image: np.ndarray) -> str:
        """识别图像中的任意文本"""
        if image is None or image.size == 0:
            return ""

        if self.reader:
            try:
                results = self.reader.readtext(image, detail=0)
                return "".join(results).strip()
            except Exception as e:
                logger.error(f"OCR 文本提取失败: {e}")
                return ""
        return ""

    def extract_number(self, image: np.ndarray) -> Optional[int]:
        """专门提取图像中的整数数值 (如金币、等级、血量)"""
        if image is None or image.size == 0:
            return None

        text = self.extract_text(self.preprocess_for_digits(image))
        digits = re.findall(r"\d+", text)
        if digits:
            try:
                return int(digits[0])
            except ValueError:
                return None
        return None

    def extract_stage(self, image: np.ndarray) -> Optional[str]:
        """识别回合阶段 (如 2-1, 3-5, 4-2)"""
        if image is None or image.size == 0:
            return None

        text = self.extract_text(image)
        match = re.search(r"(\d-\d)", text)
        if match:
            return match.group(1)
        return None
