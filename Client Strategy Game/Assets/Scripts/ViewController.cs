using System.Collections.Generic;
using UnityEngine;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;


public class ViewController : MonoBehaviour {

    private bool draw;
    private Vector2 startPos;
    private Vector2 endPos;
    private Rect rect;
    public GUISkin skin;

    NetworkManager networkManager;
    int countOfCells;
    int countOfUnits;

    Dictionary<int, Transform> selectedUnits;
    Dictionary<int, Transform> allUnits;
    Dictionary<int, List<Point>> unitsPaths;

    private bool isWalking;
    private float startTime;
    Dictionary<int, DataForMove> MoveDataList;
    float journeyLength = 1;
    private StringBuilder sendData;

    void Start () {

        selectedUnits = new Dictionary<int, Transform>();
        unitsPaths = new Dictionary<int, List<Point>>();
        MoveDataList = new Dictionary<int, DataForMove>();
        allUnits = new Dictionary<int, Transform>();
        sendData = new StringBuilder();
        isWalking = false;
        startTime = Time.time;
        networkManager = new NetworkManager(this);
        draw = false;
    }

    void Update()
    {        
        //по левому клику выбираем юнита или начинаем рисовать рамку для выбора
        if (Input.GetMouseButtonDown(0) && !isWalking)
        {
            //очищаем список выбранных юнитов
            CleanSelectedUnits();

            //начинаем рисовать рамку
            startPos = Input.mousePosition;
            draw = true;

            //выбираем юнит, на которого кликнули
            SelectUnit();
        }

        //при отпускании левой кнопки мыши заканчиваем рисовать рамку
        if (Input.GetMouseButtonUp(0) && !isWalking)
        {
            draw = false;
        }

        //по клику на правую кнопку мыши - перемещение
        if (Input.GetMouseButtonUp(1) && !isWalking)
        {
            RaycastHit hit;
            Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
                if (hit.transform.tag == "Cell")
                {
                    MoveUnitsTo(hit.transform.position);
                }
        }

        if (isWalking)
        {
            //рисуем движение
            //все юниты совершают шаги одновременно, пока хотя бы у одного еще есть куда идти
            if (unitsPaths.Any(entry => (entry.Value != null) && (entry.Value.Count != 0)))
            {
                foreach (KeyValuePair<int, Transform> entry in selectedUnits)
                {
                    //если юниту не нужно никуда идти, он остается на месте
                    if (unitsPaths[entry.Key] != null && unitsPaths[entry.Key].Count > 0)
                    {
                        float distCovered = (Time.time - MoveDataList[entry.Key].startTime) * 5.0f;
                        float fracJourney = distCovered / journeyLength;
                        if (fracJourney > 1) fracJourney = 1;
                        entry.Value.GetComponent<Unit>().SetRunAnimation(true);

                        //поворот юнита по направлению движения
                        Vector3 point = MoveDataList[entry.Key].endPos;
                        point.y = entry.Value.position.y;
                        entry.Value.LookAt(point);
                       
                        //перемещение юнита
                        entry.Value.position = Vector3.Lerp(MoveDataList[entry.Key].startPos, MoveDataList[entry.Key].endPos, fracJourney);

                        //если юнит дошел до точки очередного шага, она исключается из его пути
                        if (entry.Value.position == MoveDataList[entry.Key].endPos)
                        {
                            unitsPaths[entry.Key].RemoveAt(0);
                            //движение завершается, когда из пути удаляются все точки
                            if (unitsPaths[entry.Key].Count > 0)
                            {
                                MoveDataList[entry.Key] = new DataForMove
                                    (entry.Value.position, new Vector3(unitsPaths[entry.Key].First().x, 0.5f, unitsPaths[entry.Key].First().y));
                            }
                            else
                                entry.Value.GetComponent<Unit>().SetRunAnimation(false);
                        }
                    }
                }
            }
            else
            {
                isWalking = false;
                MoveDataList.Clear();
                foreach (KeyValuePair<int, Transform> entry in selectedUnits)
                    entry.Value.GetComponent<Unit>().SetRunAnimation(false);
            }
        }
    }

    void OnGUI()
    {        
        if (draw)
        {
            //очищаем список выбранных и начинаем рисовать рамку
            CleanSelectedUnits();

            endPos = Input.mousePosition;
            if (startPos == endPos)
            {
                SelectUnit();
                return;
            }

            rect = new Rect(Mathf.Min(endPos.x, startPos.x),
                            Screen.height - Mathf.Max(endPos.y, startPos.y),
                            Mathf.Max(endPos.x, startPos.x) - Mathf.Min(endPos.x, startPos.x),
                            Mathf.Max(endPos.y, startPos.y) - Mathf.Min(endPos.y, startPos.y)
                            );

            GUI.Box(rect, "");

            for (int j = 0; j < allUnits.Count; j++)
            {
                // трансформируем позицию объекта из мирового пространства, в пространство экрана
                Vector2 tmp = new Vector2(Camera.main.WorldToScreenPoint(allUnits[j].position).x, Screen.height
                    - Camera.main.WorldToScreenPoint(allUnits[j].position).y);
                // если объект находится в рамке, добавляем его в список выделенных
                if (rect.Contains(tmp)) 
                {
                    SelectUnit(allUnits[j].GetComponent<Unit>());
                }
            }
        }
    }
    
    //выделение юнита по ссылке
    private void SelectUnit (Unit unit)
    {
        unit.SetFlag(true);
        selectedUnits.Add(unit.id, unit.transform);
    }

    //выделение юнита под курсором мыши
    private void SelectUnit()
    {
        RaycastHit hit;
        Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit))
            if (hit.transform.tag == "Unit")
            {
                SelectUnit(hit.transform.GetComponent<Unit>());
            }
    }

    private void MoveUnitsTo(Vector3 destination)
    {
        if (selectedUnits.Count > 0)
        {
            sendData.Remove(0, sendData.Length);

            //подготавливаем данные для отправки на сервер
            List<int> units = new List<int>();
            foreach (KeyValuePair<int, Transform> entry in selectedUnits)
            {
                units.Add(entry.Key);
            }
            
            foreach (int unit in units)
            {
                sendData.Append(unit.ToString() + ",");
            }
            sendData.Append(destination.x.ToString() + "," + destination.z.ToString() + ",#");

            //отправляем запрос на сервер
            networkManager.SendMoveIntent(sendData.ToString());
        }
    }

    private void CleanSelectedUnits()
    {
        if ((selectedUnits != null) && (selectedUnits.Count != 0))
        {
            foreach (KeyValuePair<int, Transform> entry in selectedUnits)
            {
                entry.Value.GetComponent<Unit>().SetFlag(false);
            }
            selectedUnits.Clear();
        }
    }

    public void ExecuteCommand(string cmd)
    {
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
                GameObject unit = Instantiate((Resources.Load<GameObject>("Footman_Blue")), new Vector3(x, 0.5f, y), Quaternion.identity);
                unit.GetComponent<Unit>().id = id;
                allUnits.Add(id, unit.transform);
            }

            this.transform.position = new Vector3(countOfCells / 2, transform.position.y, transform.position.z);
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
                    DataForMove dataForMove = new DataForMove(allUnits[id].position, new Vector3(unitsPaths[id].First().x, 0.5f, unitsPaths[id].First().y));
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
