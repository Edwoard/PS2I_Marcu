
import * as express from "express";
import * as http from "http";

import * as socketIo from "socket.io";

//web socket port for communication with ReactApp
const port = process.env.PORT || 5000;
const app = express();
app.get('/', (req, res) => {
    res.send("Server is alive");
});

const server = http.createServer(app);

const io = new socketIo.Server(server, {
  cors: {
    origin: "http://localhost:3000",
    methods: ["GET", "POST"]
  }
});


io.on("connection", socket => {
  console.log("Client connected!");
  socket.on("disconnect", () => console.log("Client disconnected!"));
});


//server starts listening
server.listen(port, () => console.log(`Web server listening on port ${port} for react apps`));

export default io;