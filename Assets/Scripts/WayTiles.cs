using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WayTiles
{
    public List<OverlayTile> saveTiles = new List<OverlayTile>();

    public List<OverlayTile> FindWays(OverlayTile start, MovingManager list)
    {
        List<OverlayTile> openList = new List<OverlayTile>();
        //List<OverlayTile> closedList = new List<OverlayTile>();

        openList.Add(start);
        int lists = list.Commands[SlotType.Main].Count;

        for (int i = 0; i < lists; i++)
        {
            OverlayTile currentOverlayTile = openList.First();
            string currentword = list.Commands[SlotType.Main][0].type.ToString();

            openList.Remove(currentOverlayTile);
            list.Commands[SlotType.Main].RemoveAt(0);
            //closedList.Add(currentOverlayTile);
    
            if(list.Commands[SlotType.Main].Count == 0)
            {
                return GetFinishedList(currentOverlayTile, currentword);
            }

            var neighbourTiles = GetNeighbourTiles(currentOverlayTile, currentword);

            foreach (var neighbour in neighbourTiles)
            {
                openList.Add(neighbour);
                saveTiles.Add(neighbour);
            }
        }

        return new List<OverlayTile>();
    }

    private List<OverlayTile> GetFinishedList(OverlayTile end, string word)
    {
        OverlayTile currentTile = end;

        var neighbourTiles = GetNeighbourTiles(currentTile, word);

        foreach (var neighbour in neighbourTiles)
        {
            saveTiles.Add(neighbour);
        }

        return saveTiles;
    }

    private List<OverlayTile> GetNeighbourTiles(OverlayTile currenOverlayTile, string move)
    {
        var map = MapManager.Instance.map;

        List<OverlayTile> neighbours = new List<OverlayTile>();

        switch (move)
        {
            case "Forward":
                Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y + 1);
                Vector2Int currentLocation = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);

                if (map.ContainsKey(locationToCheck))
                {
                    neighbours.Add(map[locationToCheck]);
                }
                else
                {
                    neighbours.Add(map[currentLocation]);
                }
                break;

            case "Right":
                locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x + 1, currenOverlayTile.gridLocation.y);
                currentLocation = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);

                if (map.ContainsKey(locationToCheck))
                {
                    neighbours.Add(map[locationToCheck]);
                }
                else
                {
                    neighbours.Add(map[currentLocation]);
                }
                break;

            case "Left":
                locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x - 1, currenOverlayTile.gridLocation.y);
                currentLocation = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);

                if (map.ContainsKey(locationToCheck))
                {
                    neighbours.Add(map[locationToCheck]);
                }
                else
                {
                    neighbours.Add(map[currentLocation]);
                }
                break;

            case "Bottom":
                locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y - 1);
                currentLocation = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);

                if (map.ContainsKey(locationToCheck))
                {
                    neighbours.Add(map[locationToCheck]);
                }
                else
                {
                    neighbours.Add(map[currentLocation]);
                }
                break;

            case null:
                locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);
                neighbours.Add(map[locationToCheck]);
            
                break;
            
            case "Attack":
                locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);
                neighbours.Add(map[locationToCheck]);
            
                break;
        }

        /*if (move == "Forward")
        {
            Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y + 1);

            if (map.ContainsKey(locationToCheck))
            {
                neighbours.Add(map[locationToCheck]);
            }
        }
        
        else if (move == "Right")
        {
            Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x + 1, currenOverlayTile.gridLocation.y);

            if (map.ContainsKey(locationToCheck))
            {
                neighbours.Add(map[locationToCheck]);
            }
        }

        else if (move == "Left")
        {
            Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x - 1, currenOverlayTile.gridLocation.y);

            if (map.ContainsKey(locationToCheck))
            {
                neighbours.Add(map[locationToCheck]);
            }
        }

        else if (move == "Bottom")
        {
            Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y - 1);

            if (map.ContainsKey(locationToCheck))
            {
                neighbours.Add(map[locationToCheck]);
            }
        }

        else if (move == "Attack")
        {
            Vector2Int locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y);

            if (map.ContainsKey(locationToCheck))
            {
                neighbours.Add(map[locationToCheck]);
            }
        }

        //Bottom
        locationToCheck = new Vector2Int(currenOverlayTile.gridLocation.x, currenOverlayTile.gridLocation.y - 1);

        if (map.ContainsKey(locationToCheck))
        {
            neighbours.Add(map[locationToCheck]);
        }*/
        
        return neighbours;
    }
}
