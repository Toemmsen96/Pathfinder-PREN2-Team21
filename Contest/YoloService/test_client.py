import asyncio
import websockets
import json

async def test_websocket():
    uri = "ws://localhost:5000/ws"
    
    try:
        async with websockets.connect(uri) as websocket:
            print("Connected to WebSocket server")
            
            # Test ping command
            ping_message = json.dumps({"command": "ping"})
            await websocket.send(ping_message)
            response = await websocket.recv()
            print(f"Ping response: {response}")
            
            # Test status command
            status_message = json.dumps({"command": "status"})
            await websocket.send(status_message)
            response = await websocket.recv()
            print(f"Status response: {response}")
            
            # Test detection command
            # Replace with your actual image path
            detect_message = json.dumps({
                "command": "detect",
                "image_path": "images",  # Update this path
                "confidence": 0.5,
                "output_path": "output",
                "save_json": True,
                "no_draw": False
            })
            
            print("Sending detection request...")
            await websocket.send(detect_message)
            response = await websocket.recv()
            print(f"Detection response: {response}")
            
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    asyncio.run(test_websocket())