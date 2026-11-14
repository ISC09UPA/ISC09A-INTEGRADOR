class Motion:
    def __init__(self):
        self.previous = False

    def update(self, value):
        if type(value) is not bool:
            raise ValueError("Movimiento debe ser booleano")
        rising = value and not self.previous
        self.previous = value
        return rising
