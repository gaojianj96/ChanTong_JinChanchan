"""
窗口查找与管理模块：定位模拟器或客户端窗口句柄与屏幕区域
"""
from typing import Optional, Tuple, List
import win32gui
import win32con
from loguru import logger


class WindowManager:
    """管理游戏窗口的定位、尺寸监测与焦点激活"""

    def __init__(self, target_titles: Optional[List[str]] = None):
        self.target_titles = target_titles or [
            "金铲铲之战",
            "MuMu模拟器",
            "雷电模拟器",
            "夜神模拟器",
            "Tencent Game Assistant",
        ]
        self.hwnd: Optional[int] = None
        self.last_rect: Optional[Tuple[int, int, int, int]] = None

    def find_game_window(self) -> Optional[int]:
        """枚举当前所有窗口，查找匹配标题的窗口句柄"""
        matched_hwnds = []

        def enum_windows_callback(hwnd, extra):
            if win32gui.IsWindowVisible(hwnd):
                title = win32gui.GetWindowText(hwnd)
                if any(target in title for target in self.target_titles):
                    matched_hwnds.append((hwnd, title))
            return True

        win32gui.EnumWindows(enum_windows_callback, None)

        if not matched_hwnds:
            logger.warning("未找到匹配的游戏或模拟器窗口")
            self.hwnd = None
            return None

        # 默认取第一个匹配窗口
        self.hwnd, matched_title = matched_hwnds[0]
        logger.info(f"成功绑定窗口: {matched_title} (HWND: {self.hwnd})")
        return self.hwnd

    def get_window_rect(self) -> Optional[Tuple[int, int, int, int]]:
        """
        获取当前窗口在屏幕上的坐标 (left, top, right, bottom)
        """
        if not self.hwnd or not win32gui.IsWindow(self.hwnd):
            if not self.find_game_window():
                return None

        try:
            rect = win32gui.GetWindowRect(self.hwnd)
            self.last_rect = rect
            return rect
        except Exception as e:
            logger.error(f"获取窗口坐标失败: {e}")
            return None

    def bring_to_front(self) -> bool:
        """将游戏窗口置顶前台"""
        if self.hwnd and win32gui.IsWindow(self.hwnd):
            try:
                win32gui.ShowWindow(self.hwnd, win32con.SW_RESTORE)
                win32gui.SetForegroundWindow(self.hwnd)
                return True
            except Exception as e:
                logger.error(f"激活窗口失败: {e}")
        return False
