from client import Client

client = Client()
def log(isError, message):
	if isError:
		print 'ERROR:', message
	else:
		print message
client.log = log
client.requestLogs()
client.listen()
