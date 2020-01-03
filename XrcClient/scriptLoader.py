from client import Client
import sys

client = Client()
client.loadScript(sys.argv[1], file(sys.argv[1]).read())
