using System.Collections.Generic;
using UnityEngine;
using System.Collections.ObjectModel;
using System.Linq;

public class ViewController : MonoBehaviour {

    Server server;

    Dictionary<int, Transform> selectedUnits;
    Dictionary<int, List<Point>> unitsPaths;

    bool isWalking;
    private float startTime;
    Dictionary<int, DataForMove> MoveDataList;
    float journeyLength = 1;

    // Use this for initialization
    void Awake () {

        server = new Server();
        server.GetStartData();

        selectedUnits = new Dictionary<int, Transform>();
        unitsPaths = new Dictionary<int, List<Point>>();
        MoveDataList = new Dictionary<int, DataForMove>();
        isWalking = false;
        startTime = Time.time;

        //генерируем поле
        for (int i = 0; i < server.countOfCells; i++)
            for (int j = 0; j < server.countOfCells; j++)
            {
                GameObject Cube = Instantiate(Resources.Load<GameObject>("Cube"), new Vector3(i, 0, j), Quaternion.identity);
            }

        //генерируем юнитов
        for (int i = 0; i < server.countOfUnits; i++)
        {
            GameObject unit = Instantiate(Resources.Load<GameObject>("Unit"), new Vector3(server.unitsPositions[i].x, 1, server.unitsPositions[i].y), Quaternion.identity);
            unit.GetComponent<Unit>().id = i;
        }

    }

    // Update is called once per frame
    void Update () {

        if (Input.GetMouseButtonDown(0) && !isWalking)
        {
            RaycastHit hit;
            Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
                if (hit.transform.tag == "Unit")
                {
                    SelectUnit(hit.transform.GetComponent<Unit>());
                }
                else if (hit.transform.tag == "Cell")
                {
                    MoveUnitsTo(hit.transform.position);
                }
        }
        
        if (isWalking)
        {
            //рисуем движение
            if (unitsPaths.Any(entry => (entry.Value != null) && (entry.Value.Count != 0)))
            {
                foreach (KeyValuePair<int, Transform> entry in selectedUnits)
                {
                    if (unitsPaths[entry.Key] != null && unitsPaths[entry.Key].Count > 0)
                    {
                        float distCovered = (Time.time - MoveDataList[entry.Key].startTime) * 5.0f;
                        float fracJourney = distCovered / journeyLength;
                        if (fracJourney > 1) fracJourney = 1;

                        entry.Value.position = Vector3.Lerp(MoveDataList[entry.Key].startPos, MoveDataList[entry.Key].endPos, fracJourney);

                        if (entry.Value.position == MoveDataList[entry.Key].endPos)
                        {
                            unitsPaths[entry.Key].RemoveAt(0);

                            if (unitsPaths[entry.Key].Count > 0)
                            {
                                MoveDataList[entry.Key] = new DataForMove
                                    (entry.Value.position, new Vector3(unitsPaths[entry.Key].First().x, 1, unitsPaths[entry.Key].First().y));
                            }
                        }
                    }
                }
            }
            else
            {
                isWalking = false;
                MoveDataList.Clear();
            }
                

        }
    }

    private void SelectUnit (Unit unit)
    {
        unit.ChangeFlag();
        if (selectedUnits.ContainsKey(unit.id))
            selectedUnits.Remove(unit.id);
        else
            selectedUnits.Add(unit.id, unit.transform);
    }

    private void MoveUnitsTo(Vector3 destination)
    {
        if (selectedUnits.Count > 0)
        {
            //запрашиваем информацию о траекториях движения
            List<int> units = new List<int>();
            foreach (KeyValuePair<int, Transform> entry in selectedUnits)
            {
                units.Add(entry.Key);
            }            
            unitsPaths = server.ProvideUnitPaths(units, new Point((int)destination.x, (int)destination.z));

            //сохраняем данные для передвижения юнитов
            foreach (int id in units)
            {
                if ((unitsPaths[id] != null) && (unitsPaths[id].Count != 0))
                {
                    DataForMove data = new DataForMove(selectedUnits[id].position, new Vector3(unitsPaths[id].First().x, 1, unitsPaths[id].First().y));
                    MoveDataList.Add(id, data);
                }
            }

            isWalking = true;
        }
    }

    
}

public class Server
{
    public int countOfCells;
    public int countOfUnits;
    public Dictionary<int, Point> unitsPositions;
    public int[,] field;

    public void GetStartData ()
    {
        countOfCells = Random.Range(7, 12);
        countOfUnits = Random.Range(1, 5);
        unitsPositions = new Dictionary<int, Point>();
        field = new int[countOfCells, countOfCells];

        for (int i = 0; i < countOfCells; i++)
            for (int j = 0; j < countOfCells; j++)
            {
                //0 - незанятая ячейка, 1 - занятая
                field[i, j] = 0;
            }

        //расставляем юнитов и отмечаем на карте их расположение
        for (int i = 0; i < countOfUnits; i++)
        {
            unitsPositions.Add(i, new Point(Random.Range(0, countOfCells), Random.Range(0, countOfCells)));
            field[unitsPositions[i].x, unitsPositions[i].y] = 1;
        }
    }

    public Dictionary<int, List<Point>> ProvideUnitPaths (List<int> unitsToMove, Point destination)
    {
        //пути без учета пересечений юнитов
        Dictionary<int, List<Point>> initialPaths = new Dictionary<int, List<Point>>();
        Dictionary<int, List<Point>> finalPaths = new Dictionary<int, List<Point>>();
        foreach (int unitID in unitsToMove)
        {
            initialPaths.Add(unitID, FindPath(unitsPositions[unitID], destination));
            finalPaths.Add(unitID, new List<Point>());
        }

        //прогон перемещений на поиск пересечений в пути и нахождение окончательного пути для каждого каджита
        while(initialPaths.Any(entry => (entry.Value != null) && (entry.Value.Count != 0)))
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
        return finalPaths;
    }




    public List<Point> FindPath(Point start, Point goal)
    {
        // Шаг 1.
        var closedSet = new Collection<PathNode>();
        var openSet = new Collection<PathNode>();
        // Шаг 2.
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
            // Шаг 3.
            var currentNode = openSet.OrderBy(node =>
              node.EstimateFullPathLength).First();
            // Шаг 4.
            if (currentNode.Position == goal)
                return GetPathForNode(currentNode);
            // Шаг 5.
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
            // Шаг 6.
            foreach (var neighbourNode in GetNeighbours(currentNode, goal, field))
            {
                // Шаг 7.
                if (closedSet.Count(node => node.Position == neighbourNode.Position) > 0)
                    continue;
                var openNode = openSet.FirstOrDefault(node =>
                  node.Position == neighbourNode.Position);
                // Шаг 8.
                if (openNode == null)
                    openSet.Add(neighbourNode);
                else
                  if (openNode.PathLengthFromStart > neighbourNode.PathLengthFromStart)
                {
                    // Шаг 9.
                    openNode.CameFrom = currentNode;
                    openNode.PathLengthFromStart = neighbourNode.PathLengthFromStart;
                }
            }
        }
        // Шаг 10.
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

public struct DataForMove
{
    public float startTime;
    public Vector3 startPos, endPos;
    public DataForMove(Vector3 startPos, Vector3 endPos)
    {
        startTime = Time.time;
        this.startPos = startPos;
        this.endPos = endPos;
    }
}
