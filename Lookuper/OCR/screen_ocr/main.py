
import base64
import cv2
import numpy as np
import easyocr
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional

reader = easyocr.Reader(['en'])

def decode_image(base64_string: str) -> np.ndarray:
    img_data = base64.b64decode(base64_string)
    np_arr = np.frombuffer(img_data, np.uint8)
    return cv2.imdecode(np_arr, cv2.IMREAD_COLOR)

def is_cursor_on_word(cursor_x: int, cursor_y: int, words: list) -> Optional[str]:
    max_offset = 10
    for offset in range(max_offset + 1):
        for word, x_min, y_min, x_max, y_max in words:
            if x_min - offset <= cursor_x <= x_max + offset and y_min - offset <= cursor_y <= y_max + offset:
                return word
    return None


class ImageRequest(BaseModel):
    image: str
    x: int
    y: int


app = FastAPI()

@app.post("/process_image/")
async def process_image(data: ImageRequest):
    try:
        img = decode_image(data.image)
        results = reader.readtext(img)

        words = [(text, x1, y1, x2, y2) for (bbox, text, _) in results for (x1, y1), (x2, y2), (x3, y3), (x4, y4) in
                 [bbox]]

        found_word = is_cursor_on_word(data.x, data.y, words)

        if found_word:
            return {"word": found_word}
        else:
            return {"word": None}

    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Error processing the image: {str(e)}")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=6969)
