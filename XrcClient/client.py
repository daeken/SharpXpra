import struct
from socket import *

class Client(object):
	def __init__(self):
		s = socket(AF_INET, SOCK_DGRAM)
		s.bind(('', 31337))
		d, e = s.recvfrom(1024)
		host = e[0]
		port, dlen = struct.unpack('<HI', d[:6])
		name = d[6:6+dlen]

		self.log = lambda isError, msg: None

		print 'Connecting to', name

		sock = self.sock = socket(AF_INET, SOCK_STREAM)
		sock.connect((host, port))

	def listen(self):
		while True:
			size, opcode = struct.unpack('<II', self.recv(8))
			data = self.recv(size) if size > 0 else ''

			if opcode == 1:
				print 'Ping'
				self.send(2)
			elif opcode == 2001:
				print size
				print `data`
				self.log(ord(data[0]) != 0, data[1:])
			else:
				print 'Unknown packet with opcode', opcode, 'and size', size

	def recv(self, length):
		data = ''
		off = 0
		while off < length:
			recv = self.sock.recv(length - off)
			data += recv
			off += len(recv)
		return data

	def send(self, opcode, data=None):
		self.sock.send(struct.pack('<II', len(data) if data is not None else 0, opcode))
		if data is not None:
			self.sock.send(data)

	def runScript(self, code):
		self.send(1001, code)

	def loadScript(self, fn, code):
		self.send(1002, struct.pack('<I', len(fn)) + fn + code)

	def requestLogs(self):
		self.send(1004)
