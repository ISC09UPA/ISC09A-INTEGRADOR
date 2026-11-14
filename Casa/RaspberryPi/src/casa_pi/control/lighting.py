def automatic(current, value, low, high):
    if value < low:
        return True
    if value > high:
        return False
    return current
