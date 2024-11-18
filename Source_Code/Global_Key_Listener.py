import os
from pynput import keyboard

PIPE_NAME = r'\\.\pipe\CommandPipe'

# Drawing settings
draw_color = "Black"
brush_size = 1

def send_pipe_message(message):
    """Sends a message via the named pipe."""
    try:
        with open(PIPE_NAME, 'w') as pipe:
            pipe.write(message)
            pipe.flush()
        print(f"Message sent: {message}")
    except Exception as e:
        print(f"Error sending message: {e}")

def on_press(key):
    """Handles key press events."""
    try:
        if key.char == 'm':
            print("M key detected. Sending toggle command...")
            send_pipe_message("TOGGLE_MODE")
        elif key.char == 'c':
            print("C key detected. Sending clear command...")
            send_pipe_message("CLEAR")
    except AttributeError:
        # Handle special keys that don't have a char attribute
        pass


if __name__ == "__main__":
    # Prompt the user to set drawing options
    print("Configure your drawing settings:")
    # Select draw color
    color_map = {"1": "Black", "2": "Red", "3": "Green", "4": "Blue"}
    print("Select your draw color:")
    print("1. Black (default)")
    print("2. Red")
    print("3. Green")
    print("4. Blue")
    color_choice = input("Enter the number corresponding to your color choice: ")
    draw_color = color_map.get(color_choice, "Black")

    # Select brush size
    while True:
        try:
            brush_size = int(input("Enter brush size (1-10, default is 1): "))
            if 1 <= brush_size <= 10:
                break
            else:
                print("Please enter a number between 1 and 10.")
        except ValueError:
            print("Invalid input. Please enter a number.")

    print(f"Selected color: {draw_color}, brush size: {brush_size}")

    # Send the initial color and size settings immediately
    settings_message = f"SETTINGS|COLOR:{draw_color}|SIZE:{brush_size}"
    send_pipe_message(settings_message)

    print("Listening for the 'M' key (Toggle Mode), 'C' key (Clear Image)")
    with keyboard.Listener(on_press=on_press) as listener:
        listener.join()
