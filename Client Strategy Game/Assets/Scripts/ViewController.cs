using System.Collections.Generic;
using UnityEngine;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;


public class ViewController : MonoBehaviour {

    NetworkManager networkManager;
    int countOfCells;
    int countOfUnits;

    Dictionary<int, Transform> selectedUnits;
    Dictionary<int, Transform> allUnits;
    Dictionary<int, List<Point>> unitsPaths;

    bool isWalking;
    private float startTime;
    Dictionary<int, DataForMove> MoveDataList;
    float journeyLength = 1;
    public StringBuilder sendData;

    public bool haveDataToSend = false;

    // Use this for initialization
    void Awake () {
        selectedUnits = new Dictionary<int, Transform>();
        unitsPaths = new Dictionary<int, List<Point>>();
        MoveDataList = new Dictionary<int, DataForMove>();
        allUnits = new Dictionary<int, Transform>();
        sendData = new StringBuilder();
        isWalking = false;
        startTime = Time.time;
        networkManager = new NetworkManager(this);
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

            sendData.Remove(0, sendData.Length);
            //складываем все данные в одну строку
            foreach (int unit in units)
            {
                sendData.Append(unit.ToString() + ",");
            }
            sendData.Append(destination.x.ToString() + "," + destination.z.ToString() + ",#");

            networkManager.SendMoveIntent(sendData.ToString());
        }
    }

    public void ExecuteCommand(string cmd)
    {
        Debug.Log(cmd);
        string[] data = cmd.Split(',');
        //если первый символ 1 - пришли начальные данные
        if (data[0].Equals("1"))
        {
            countOfCells = int.Parse(data[1]);
            countOfUnits = int.Parse(data[2]);

            //генерируем поле
            for (int i = 0; i < countOfCells; i++)
                for (int j = 0; j < countOfCells; j++)
                {
                    GameObject Cube = Instantiate(Resources.Load<GameObject>("Cube"), new Vector3(i, 0, j), Quaternion.identity);
                }

            //генерируем юнитов
            for (int k = 0; k < countOfUnits; k++)
            {
                int id = int.Parse(data[3 + 3 * k]);
                int x = int.Parse(data[3 + 3 * k + 1]);
                int y = int.Parse(data[3 + 3 * k + 2]);
                GameObject unit = Instantiate(Resources.Load<GameObject>("Unit"), new Vector3(x, 1, y), Quaternion.identity);
                unit.GetComponent<Unit>().id = id;
                allUnits.Add(id, unit.transform);
            }
        }
        //если первый символ 2 - пришли данные о маршрутах выделенных юнитов
        else if (data[0].Equals("2"))
        {
            unitsPaths.Clear();
            for (int i = 1; i < data.Length - 1; i++)
            {
                string[] unitData = data[i].Split('$');
                List<Point> path = new List<Point>();
                for (int j = 1; j < unitData.Length; j += 2)
                {
                    path.Add(new Point(int.Parse(unitData[j]), int.Parse(unitData[j + 1])));
                }
                unitsPaths.Add(int.Parse(unitData[0]), path);
            }

            //сохраняем данные для передвижения юнитов
            foreach (KeyValuePair<int, List<Point>> entry in unitsPaths)
            {
                int id = entry.Key;
                if ((unitsPaths[id] != null) && (unitsPaths[id].Count != 0))
                {
                    DataForMove dataForMove = new DataForMove(allUnits[id].position, new Vector3(unitsPaths[id].First().x, 1, unitsPaths[id].First().y));
                    MoveDataList.Add(id, dataForMove);
                }
            }

            isWalking = true;
        }
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
