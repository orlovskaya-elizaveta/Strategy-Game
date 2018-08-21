using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// State object for reading client data asynchronously  
public class StateObject
{
    // Client  socket.  
    public Socket workSocket = null;
    // Size of receive buffer.  
    public const int BufferSize = 1024;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.  
    public StringBuilder sb = new StringBuilder();
    public ManualResetEvent receiveDone = new ManualResetEvent(false);
    public ServerLogic serverLogic = new ServerLogic();
}

public class AsynchronousSocketListener
{
    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);
    static string receivedData;
    private static String response = String.Empty;
    public ServerLogic serverLogic = new ServerLogic();
    public AsynchronousSocketListener()
    {
    }

    public static void StartListening()
    {
        // Establish the local endpoint for the socket.  
        // The DNS name of the computer  
        // running the listener is "host.contoso.com".  
        IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
        IPAddress ipAddress = ipHostInfo.AddressList[0];
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(ipAddress.AddressFamily,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        allDone.Set();

        // Get the socket that handles the client request.  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;

        Send(handler, state.serverLogic.GetStartData());

        while (true)
        {
            state.sb.Clear();
            state.receiveDone.Reset();
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            state.receiveDone.WaitOne();
        }
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;

        // Retrieve the state object and the handler socket  
        // from the asynchronous state object.  
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket.   
        int bytesRead = handler.EndReceive(ar);

        if (bytesRead > 0)
        {
            // There  might be more data, so store the data received so far.  
            state.sb.Append(Encoding.ASCII.GetString(
                state.buffer, 0, bytesRead));

            // Check for end-of-file tag. If it is not there, read   
            // more data.  
            content = state.sb.ToString();
            if (content.Contains("#"))
            {
                // All the data has been read from the   
                // client. Display it on the console.  
                Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                    content.Length, content);
                // Echo the data back to the client.  
                Send(handler, state.serverLogic.ProvideUnitPaths(content));
                state.receiveDone.Set();
            }
            else
            {
                // Not all data received. Get more.  
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }
        }
    }

    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main(String[] args)
    {
        StartListening();
        return 0;
    }
}
    


public class ServerLogic
{
    public int countOfCells;
    public int countOfUnits;
    public Dictionary<int, Point> unitsPositions;
    public int[,] field;

    public string GetStartData()
    {
        Random random = new Random(); 
        countOfCells = random.Next(7, 12);
        countOfUnits = random.Next(1, 5);
        unitsPositions = new Dictionary<int, Point>();
        field = new int[countOfCells, countOfCells];

        //генерируем карту
        for (int i = 0; i < countOfCells; i++)
            for (int j = 0; j < countOfCells; j++)
            {
                //0 - незанятая ячейка, 1 - занятая
                field[i, j] = 0;
            }

        //расставляем юнитов и отмечаем на карте их расположение
        for (int i = 0; i < countOfUnits; i++)
        {
            int x = random.Next(0, countOfCells);
            int y = random.Next(0, countOfCells);
            //проверяем, не занята ли уже эта клетка 
            while (field[x, y] == 1)
            {
                x = random.Next(0, countOfCells);
                y = random.Next(0, countOfCells);
            }
            unitsPositions.Add(i, new Point(random.Next(0, countOfCells), random.Next(0, countOfCells)));
            field[unitsPositions[i].x, unitsPositions[i].y] = 1;
        }

        //складываем все данные в одну строку
        StringBuilder startData = new StringBuilder();

        //первый символ в строке - код команды. 1 - начальные данные, 2 - пути для перемещения юнитов
        startData.Append("1," + countOfCells.ToString() + "," + countOfUnits.ToString());
        foreach (KeyValuePair<int, Point> unit in unitsPositions)
        {
            startData.Append("," + unit.Key.ToString() + "," + unit.Value.x.ToString() + "," + unit.Value.y.ToString());
        }
        startData.Append(",#");
        return startData.ToString();
    }

    public string ProvideUnitPaths(string receivedData)
    {
        List<int> unitsToMove = new List<int>();
        Point destination = new Point();

        string[] data = receivedData.Split(',');
        for (int i = 0; i < data.Count() - 3; i++)
        {
            unitsToMove.Add(int.Parse(data[i]));
        }
        destination.x = int.Parse(data[data.Count() - 3]);
        destination.y = int.Parse(data[data.Count() - 2]);
        
        //пути без учета пересечений юнитов
        Dictionary<int, List<Point>> initialPaths = new Dictionary<int, List<Point>>();
        Dictionary<int, List<Point>> finalPaths = new Dictionary<int, List<Point>>();
        foreach (int unitID in unitsToMove)
        {
            initialPaths.Add(unitID, FindPath(unitsPositions[unitID], destination));
            finalPaths.Add(unitID, new List<Point>());
        }

        //прогон перемещений на поиск пересечений в пути и нахождение окончательного пути для каждого каджита
        while (initialPaths.Any(entry => (entry.Value != null) && (entry.Value.Count != 0)))
        {
            foreach (int unitID in unitsToMove)
            {
                List<Point> thisWay = initialPaths[unitID];
                if ((thisWay == null) || (thisWay.Count == 0))
                {
                    continue;
                }

                Point nextStep = thisWay.First();

                //если клетка занята на этом шаге - перестраиваем путь
                if (field[nextStep.x, nextStep.y] == 1)
                {
                    initialPaths[unitID] = FindPath(unitsPositions[unitID], destination);
                    //если дальше идти некуда - останавливаемся
                    if (initialPaths[unitID] == null)
                        continue;
                }
                //отмечаем на карте новую занятую клетку и освобождаем старую
                field[nextStep.x, nextStep.y] = 1;
                field[unitsPositions[unitID].x, unitsPositions[unitID].y] = 0;
                //перемещаем юнита 
                unitsPositions[unitID] = nextStep;
                //переносим сделанный шаг в окончательный путь
                finalPaths[unitID].Add(nextStep);
                initialPaths[unitID].RemoveAt(0);
            }
        }

        //преобразуем данные в одну строку для отправки клиенту
        StringBuilder sendData = new StringBuilder();
        sendData.Append("2,");
        foreach (KeyValuePair<int, List<Point>> entry in finalPaths)
        {
            sendData.Append(entry.Key.ToString());
            for (int i = 0; i < entry.Value.Count; i++)
            {
                sendData.Append("$" + entry.Value[i].x.ToString() + "$" + entry.Value[i].y.ToString());
            }
            sendData.Append(",");
        }
        sendData.Append("#");
        return sendData.ToString();
    }

    public List<Point> FindPath(Point start, Point goal)
    {
        //алгоритм А* для поиска пути
        var closedSet = new Collection<PathNode>();
        var openSet = new Collection<PathNode>();
        PathNode startNode = new PathNode()
        {
            Position = start,
            CameFrom = null,
            PathLengthFromStart = 0,
            HeuristicEstimatePathLength = GetHeuristicPathLength(start, goal)
        };
        openSet.Add(startNode);
        while (openSet.Count > 0)
        {
            var currentNode = openSet.OrderBy(node =>
              node.EstimateFullPathLength).First();
            if (currentNode.Position == goal)
                return GetPathForNode(currentNode);
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
            foreach (var neighbourNode in GetNeighbours(currentNode, goal, field))
            {
                if (closedSet.Count(node => node.Position == neighbourNode.Position) > 0)
                    continue;
                var openNode = openSet.FirstOrDefault(node =>
                  node.Position == neighbourNode.Position);
                if (openNode == null)
                    openSet.Add(neighbourNode);
                else
                  if (openNode.PathLengthFromStart > neighbourNode.PathLengthFromStart)
                {
                    openNode.CameFrom = currentNode;
                    openNode.PathLengthFromStart = neighbourNode.PathLengthFromStart;
                }
            }
        }
        return null;
    }

    private static int GetDistanceBetweenNeighbours()
    {
        return 1;
    }

    private static int GetHeuristicPathLength(Point from, Point to)
    {
        return System.Math.Abs(from.x - to.x) + System.Math.Abs(from.y - to.y);
    }

    private Collection<PathNode> GetNeighbours(PathNode pathNode, Point goal, int[,] field)
    {
        var result = new Collection<PathNode>();

        // Соседними точками являются соседние по стороне клетки.
        Point[] neighbourPoints = new Point[4];
        neighbourPoints[0] = new Point(pathNode.Position.x + 1, pathNode.Position.y);
        neighbourPoints[1] = new Point(pathNode.Position.x - 1, pathNode.Position.y);
        neighbourPoints[2] = new Point(pathNode.Position.x, pathNode.Position.y + 1);
        neighbourPoints[3] = new Point(pathNode.Position.x, pathNode.Position.y - 1);

        foreach (var point in neighbourPoints)
        {
            // Проверяем, что не вышли за границы карты.
            if (point.x < 0 || point.x >= field.GetLength(0))
                continue;
            if (point.y < 0 || point.y >= field.GetLength(1))
                continue;
            // Проверяем, что по клетке можно ходить.
            if (field[point.x, point.y] == 1)
                continue;
            // Заполняем данные для точки маршрута.
            var neighbourNode = new PathNode()
            {
                Position = point,
                CameFrom = pathNode,
                PathLengthFromStart = pathNode.PathLengthFromStart +
                GetDistanceBetweenNeighbours(),
                HeuristicEstimatePathLength = GetHeuristicPathLength(point, goal)
            };
            result.Add(neighbourNode);
        }
        return result;
    }

    private List<Point> GetPathForNode(PathNode pathNode)
    {
        var result = new List<Point>();
        var currentNode = pathNode;
        while (currentNode != null)
        {
            result.Add(currentNode.Position);
            currentNode = currentNode.CameFrom;
        }
        result.Reverse();
        result.RemoveAt(0);
        return result;
    }
}

public struct Point
{
    public int x, y;
    public Point(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static bool operator ==(Point p1, Point p2)
    {
        return (p1.x == p2.x) && (p1.y == p2.y);
    }
    public static bool operator !=(Point p1, Point p2)
    {
        return (p1.x != p2.x) || (p1.y != p2.y);
    }
}

public class PathNode
{
    // Координаты точки на карте.
    public Point Position { get; set; }
    // Длина пути от старта (G).
    public int PathLengthFromStart { get; set; }
    // Точка, из которой пришли в эту точку.
    public PathNode CameFrom { get; set; }
    // Примерное расстояние до цели (H).
    public int HeuristicEstimatePathLength { get; set; }
    // Ожидаемое полное расстояние до цели (F).
    public int EstimateFullPathLength
    {
        get
        {
            return this.PathLengthFromStart + this.HeuristicEstimatePathLength;
        }
    }
}