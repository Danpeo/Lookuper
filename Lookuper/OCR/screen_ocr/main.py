#
#
# import sys
# import json
# import base64
# import cv2
# import numpy as np
# import easyocr
#
# reader = easyocr.Reader(['en'])
#
# def decode_image(base64_string):
#     img_data = base64.b64decode(base64_string)
#     np_arr = np.frombuffer(img_data, np.uint8)
#     return cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
#
# def is_cursor_on_word(cursor_x, cursor_y, words):
#     # Проверка, попадает ли мышь в прямоугольник слова с постепенным увеличением отступа
#     max_offset = 10  # Максимальный отступ в пикселях
#     for offset in range(max_offset + 1):  # Начинаем с 0 до 10 (включительно)
#         for word, x_min, y_min, x_max, y_max in words:
#             if x_min - offset <= cursor_x <= x_max + offset and y_min - offset <= cursor_y <= y_max + offset:
#                 return word
#     return None
#
# def process_image(base64_img, cursor_x, cursor_y):
#     img = decode_image(base64_img)
#     results = reader.readtext(img)
#
#     # Формируем список слов с координатами
#     words = [(text, x1, y1, x2, y2) for (bbox, text, _) in results for (x1, y1), (x2, y2), (x3, y3), (x4, y4) in [bbox]]
#     print(words)
#     # Возвращаем слово, если курсор находится в области этого слова
#     found_word = is_cursor_on_word(cursor_x, cursor_y, words)
#     print(f"FOUND WORD: {found_word}")
#     return found_word
#
# if __name__ == "__main__":
#     data = json.loads(sys.stdin.read().strip())
#     base64_img = data["image"]
#     cursor_x = data["x"]
#     cursor_y = data["y"]
#     print(f"Cursor x: {cursor_x}, y: {cursor_y}")
#     word = process_image(base64_img, cursor_x, cursor_y)
#
#     print(json.dumps({"word": word}))


import base64
import cv2
import numpy as np
import easyocr
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional

# Инициализация EasyOCR
reader = easyocr.Reader(['en'])


# Функция для декодирования изображения из base64
def decode_image(base64_string: str) -> np.ndarray:
    img_data = base64.b64decode(base64_string)
    np_arr = np.frombuffer(img_data, np.uint8)
    return cv2.imdecode(np_arr, cv2.IMREAD_COLOR)


# Функция для проверки, попадает ли курсор в область слова с постепенным увеличением отступа
def is_cursor_on_word(cursor_x: int, cursor_y: int, words: list) -> Optional[str]:
    max_offset = 10  # Максимальный отступ в пикселях
    for offset in range(max_offset + 1):  # Начинаем с 0 до 10 (включительно)
        for word, x_min, y_min, x_max, y_max in words:
            if x_min - offset <= cursor_x <= x_max + offset and y_min - offset <= cursor_y <= y_max + offset:
                return word
    return None


# Модель для данных запроса
class ImageRequest(BaseModel):
    image: str
    x: int
    y: int


# Инициализация FastAPI
app = FastAPI()


# Основная логика обработки изображения
@app.post("/process_image/")
async def process_image(data: ImageRequest):
    try:
        img = decode_image(data.image)
        results = reader.readtext(img)

        # Формируем список слов с координатами
        words = [(text, x1, y1, x2, y2) for (bbox, text, _) in results for (x1, y1), (x2, y2), (x3, y3), (x4, y4) in
                 [bbox]]

        # Проверяем, попадает ли курсор в область слова
        found_word = is_cursor_on_word(data.x, data.y, words)

        if found_word:
            return {"word": found_word}
        else:
            return {"word": None}

    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Error processing the image: {str(e)}")


if __name__ == "__main__":
    import uvicorn

    # Запуск FastAPI приложения на локальном сервере
    uvicorn.run(app, host="127.0.0.1", port=8000)
