"""
OpenRouter API 统一客户端与独立 Session/Context 管理器
"""
import os
import json
import urllib.request
import urllib.error
from typing import List, Dict, Any, Optional
from loguru import logger


class OpenRouterClient:
    """负责通过 OpenRouter API 与各个大语言模型进行安全、隔离的上下文通信"""

    def __init__(
        self,
        api_key: Optional[str] = None,
        base_url: str = "https://openrouter.ai/api/v1",
        site_url: str = "https://github.com/JianChanChan",
        site_name: str = "JianChanChan-AI",
    ):
        self.api_key = api_key or os.getenv("OPENROUTER_API_KEY", "")
        self.base_url = base_url.rstrip("/")
        self.site_url = site_url
        self.site_name = site_name

    def chat_completion(
        self,
        model: str,
        messages: List[Dict[str, str]],
        temperature: float = 0.7,
        max_tokens: int = 4096,
    ) -> str:
        """
        发送 Chat Completion 请求至指定模型
        """
        if not self.api_key:
            logger.warning("未配置 OPENROUTER_API_KEY，返回模拟响应")
            return f"[{model} (模拟响应)] 已接收上下文: {messages[-1]['content'][:100]}..."

        url = f"{self.base_url}/chat/completions"
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
            "HTTP-Referer": self.site_url,
            "X-Title": self.site_name,
        }

        payload = {
            "model": model,
            "messages": messages,
            "temperature": temperature,
            "max_tokens": max_tokens,
        }

        try:
            req = urllib.request.Request(
                url,
                data=json.dumps(payload).encode("utf-8"),
                headers=headers,
                method="POST",
            )
            with urllib.request.urlopen(req, timeout=60) as response:
                result = json.loads(response.read().decode("utf-8"))
                return result["choices"][0]["message"]["content"]
        except urllib.error.HTTPError as e:
            error_body = e.read().decode("utf-8")
            logger.error(f"OpenRouter API 请求失败 [{e.code}]: {error_body}")
            raise RuntimeError(f"OpenRouter API error {e.code}: {error_body}")
        except Exception as e:
            logger.error(f"OpenRouter 通信异常: {e}")
            raise e
