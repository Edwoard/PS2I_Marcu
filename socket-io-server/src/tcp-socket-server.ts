
import * as  net from 'net';
import io from './web-socket-server'

var HOST = '127.0.0.1';
var PORT = 5500;

// Create a server instance, and chain the listen function to it
// The function passed to net.createServer() becomes the event handler for the 'connection' event
// The sock object the callback function receives UNIQUE for each connection
let tcpServer = net.createServer(function (sock) {
  //We have a connection - a socket object is assigned to the connection automatically
  console.log('CONNECTED: ' + sock.remoteAddress + ':' + sock.remotePort);
  //Add a 'data' event handler to this instance of socket
  //data[] is the 
  sock.on('data', function (data) {
    console.log('DATA ' + sock.remoteAddress + ': ' + sock.bytesRead + ' ---> data read:  ' + data[0].toString() + ' ' + data[1].toString());
    // Emitting to all clients
    io.emit("FromAPI", data[0]); 
  });

  sock.on('close', function (data) {
    console.log('CLOSED: ' + sock.remoteAddress + ' ' + sock.remotePort);
  });

})

tcpServer.listen(PORT, HOST);
console.log('TCP server listening for simulator on ' + HOST + ':' + PORT);