import argparse
import logging
from .settings import load
from .agent import run


def main():
    parser = argparse.ArgumentParser(description="Agente casa-pi")
    parser.add_argument("--config", default="/etc/casa-pi/device.toml")
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    logging.getLogger("azure").setLevel(logging.WARNING)
    logging.getLogger("paho").setLevel(logging.WARNING)
    run(load(args.config))


if __name__ == "__main__":
    main()
