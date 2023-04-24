using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Map {
    public class Map : MonoBehaviour {
        private const float TILE_SIZE = 2;
        private const float TERRAIN_NOISE_SCALE = 0.075f;
        private const float TREE_NOISE_SCALE = 0.06f;
        private const float TERRAIN_NOISE_MULTIPLIER = 7.5f;

        private const float WATER_THRESHOLD = 0.3f;
        private const float SAND_THRESHOLD = 0.35f;
        private const float GRASS_THRESHOLD = 0.7f;
        private const float TREE_THRESHOLD = 0.05f;

        private const float BASE_LEVEL = SAND_THRESHOLD;

        private const int BASE_SIZE = 3;
        private const int BASE_TRANSITION_SIZE = 5;

        private Vector3Int BasePosition;

        private Transform Camera;
        private float NoiseOffsetTerrain;
        private float NoiseOffsetTrees;
        private float NoiseOffsetTreesDetails;

        private Dictionary<Vector3Int, Tile> Tiles;

        private Transform TilesTransform;

        [field: SerializeField] private int Seed { get; set; }
        [field: SerializeField] private Tile Water { get; set; }
        [field: SerializeField] private Tile Sand { get; set; }
        [field: SerializeField] private Tile Grass { get; set; }
        [field: SerializeField] private Tile Rock { get; set; }
        [field: SerializeField] private GameObject Trees { get; set; }
        [field: SerializeField] private List<GameObject> TreesDetails { get; set; }
        [field: SerializeField] private int Radius { get; set; }


        private void Start() {
            this.TilesTransform = this.transform.Find("Tiles");
            this.Generate();
            this.Camera = GameObject.FindGameObjectWithTag("MainCamera").transform;
        }

        private void Generate() {
            long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Debug.Log("Map generation started...");
            if (this.Seed == 0) this.Seed = Random.Range(0, 10_000_000);
            Random.InitState(this.Seed);
            Debug.Log($"Map seed: {this.Seed}");

            this.NoiseOffsetTerrain = Random.Range(0, 10_000_000);
            this.NoiseOffsetTrees = Random.Range(0, 10_000_000);
            this.NoiseOffsetTreesDetails = Random.Range(0, 10_000_000);

            Tile startingTile = this.GenerateTile(Vector3.zero, Vector3Int.zero);
            List<Tile> tiles = new() {
                startingTile
            };
            this.Tiles = new Dictionary<Vector3Int, Tile> {
                [Vector3Int.zero] = startingTile
            };

            for (int i = 0; i < this.Radius; i++) tiles = this.GenerateNeighbours(tiles);
            foreach (Tile tile in this.Tiles.Values) {
                for (int i = 0; i < 6; i++) {
                    Tile neighbour = this.Tiles.GetValueOrDefault(tile.GridPosition + AngleToCoordinates(60 * i), null);
                    if (neighbour == null) continue;
                    tile.Neighbours[AngleToDirection(60 * i)] = neighbour;
                }
            }

            Debug.Log(
                $"Map generation done:\n\t{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start}ms\n\t{this.Tiles.Keys.Count} tiles generated"
            );
        }

        private List<Tile> GenerateNeighbours(List<Tile> tiles) {
            List<Tile> neighbours = new();
            foreach (Tile tile in tiles) {
                for (int i = 0; i < 6; i++) {
                    Vector3Int gridPosition = tile.GridPosition + AngleToCoordinates(60 * i);
                    if (this.Tiles.GetValueOrDefault(gridPosition, null) != null) continue;
                    Vector3 position = tile.transform.position
                                       + new Vector3(
                                           TILE_SIZE * Mathf.Cos(60 * i * Mathf.Deg2Rad),
                                           0,
                                           TILE_SIZE * Mathf.Sin(60 * i * Mathf.Deg2Rad)
                                       );
                    Tile neighbour = this.GenerateTile(position, gridPosition);
                    this.Tiles[gridPosition] = neighbour;
                    neighbours.Add(neighbour);
                }
            }

            return neighbours;
        }

        private Tile GenerateTile(Vector3 position, Vector3Int gridPosition) {
            float terrainNoise;
            float treeNoise;

            float distanceFromBase = HexDistance(gridPosition, this.BasePosition);

            switch (distanceFromBase) {
                case <= BASE_SIZE:
                    terrainNoise = BASE_LEVEL;
                    treeNoise = 0;
                    break;
                case <= BASE_SIZE + BASE_TRANSITION_SIZE:
                    float ratio = (distanceFromBase - BASE_SIZE) / BASE_TRANSITION_SIZE;
                    terrainNoise = Mathf.PerlinNoise(
                        (position.x + this.NoiseOffsetTerrain) * TERRAIN_NOISE_SCALE,
                        (position.z + this.NoiseOffsetTerrain) * TERRAIN_NOISE_SCALE
                    );
                    treeNoise = Mathf.PerlinNoise(
                        (position.x + this.NoiseOffsetTrees) * TREE_NOISE_SCALE,
                        (position.z + this.NoiseOffsetTrees) * TREE_NOISE_SCALE
                    );

                    terrainNoise = BASE_LEVEL + (terrainNoise - BASE_LEVEL) * ratio;
                    break;
                default:
                    terrainNoise = Mathf.PerlinNoise(
                        (position.x + this.NoiseOffsetTerrain) * TERRAIN_NOISE_SCALE,
                        (position.z + this.NoiseOffsetTerrain) * TERRAIN_NOISE_SCALE
                    );
                    treeNoise = Mathf.PerlinNoise(
                        (position.x + this.NoiseOffsetTrees) * TREE_NOISE_SCALE,
                        (position.z + this.NoiseOffsetTrees) * TREE_NOISE_SCALE
                    );
                    break;
            }

            Tile tile;
            switch (terrainNoise) {
                case < WATER_THRESHOLD:
                    // Water
                    tile = Instantiate(this.Water, this.TilesTransform);
                    tile.name = $"Water - {gridPosition.ToString()}";
                    break;
                case < SAND_THRESHOLD: {
                    // Sand
                    tile = Instantiate(this.Sand, this.TilesTransform);
                    tile.name = $"Sand - {gridPosition.ToString()}";
                    Vector3 scale = new(1, 1, 1);
                    scale.y *= 1 + (terrainNoise - WATER_THRESHOLD) * TERRAIN_NOISE_MULTIPLIER;
                    tile.transform.localScale = scale;
                    break;
                }
                case < GRASS_THRESHOLD: {
                    // Grass
                    tile = Instantiate(this.Grass, this.TilesTransform);
                    tile.name = $"Grass - {gridPosition.ToString()}";
                    Vector3 scale = new(1, 1, 1);
                    scale.y *= 1 + (terrainNoise - WATER_THRESHOLD) * TERRAIN_NOISE_MULTIPLIER;
                    tile.transform.localScale = scale;

                    if (treeNoise is > 0.5f - TREE_THRESHOLD and < 0.5f + TREE_THRESHOLD) {
                        float diff = Mathf.Abs(treeNoise - 0.5f);
                        float treesDetailsNoise = Mathf.PerlinNoise(
                            (position.x + this.NoiseOffsetTreesDetails) * TREE_NOISE_SCALE,
                            (position.z + this.NoiseOffsetTreesDetails) * TREE_NOISE_SCALE
                        );
                        GameObject trees;
                        if (diff > TREE_THRESHOLD * 0.8f && treesDetailsNoise > 0.5f) {
                            trees = Instantiate(Utils.Utils.Sample(this.TreesDetails), tile.Objects.transform);
                        } else {
                            trees = Instantiate(this.Trees, tile.Objects.transform).gameObject;
                        }

                        trees.transform.eulerAngles = new Vector3(0, Random.Range(0, 360), 0);
                        tile.Walkable = false;
                    }

                    break;
                }
                default: {
                    // Rock
                    tile = Instantiate(this.Rock, this.TilesTransform);
                    tile.name = $"Rock - {gridPosition.ToString()}";
                    Vector3 scale = new(1, 1, 1);
                    scale.y *= 1 + (terrainNoise - WATER_THRESHOLD) * TERRAIN_NOISE_MULTIPLIER;
                    tile.transform.localScale = scale;
                    break;
                }
            }

            Transform tileTransform = tile.transform;
            tile.Height = tileTransform.localScale.y;
            tileTransform.position = position;
            tile.GridPosition = gridPosition;
            tile.Position = tileTransform.position;
            tile.ArrangeObjects();

            return tile;
        }

        private static Vector3Int AngleToCoordinates(float angle) {
            return angle switch {
                0 => new Vector3Int(1, 0, -1),
                60 => new Vector3Int(1, -1, 0),
                120 => new Vector3Int(0, -1, 1),
                180 => new Vector3Int(-1, 0, 1),
                240 => new Vector3Int(-1, 1, 0),
                300 => new Vector3Int(0, 1, -1),
                _ => throw new Exception("[Map:AngleToCoordinates] Invalid angle.")
            };
        }

        private static string AngleToDirection(float angle) {
            return angle switch {
                0 => "East",
                60 => "South-east",
                120 => "South-west",
                180 => "West",
                240 => "North-west",
                300 => "North-east",
                _ => throw new Exception("[Map:AngleToDirection] Invalid angle.")
            };
        }

        private static int HexDistance(Vector3 pos1, Vector3 pos2) {
            return (int)((Math.Abs(pos1.x - pos2.x) + Math.Abs(pos1.y - pos2.y) + Math.Abs(pos1.z - pos2.z)) / 2f);
        }
    }
}
