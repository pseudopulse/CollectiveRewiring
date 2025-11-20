using System;
using RoR2.EntitlementManagement;
using UnityEngine.SceneManagement;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: Wandering Chef")]
    public static class WanderingChef {
        [ConfigField("Spawn On Ending Stages", "Makes the Wandering Chef spawn on Prime Meridian, Commencement, and Void Locus. Requires Alloyed Collective to be enabled.", true)]
        private static bool SpawnOnEndingStages;
        public static void Initialize() {
            if (SpawnOnEndingStages) {
                SceneManager.activeSceneChanged += SceneChange;
            }
        }

        private static void SceneChange(Scene arg0, Scene arg1)
        {
            Vector3 position = Vector3.zero;
            Vector3 boardPosition = Vector3.zero;
            Vector3 euler = Vector3.zero;
            Vector3 boardRotation = Vector3.zero;
            Material mat = null;

            if (SceneManager.GetActiveScene().name == "moon2") {
                position = new Vector3(-370, -134, -225);
                boardPosition = new Vector3(-370, -135.3f, -225);
                boardRotation = new Vector3(270, 90, 0);
                mat = Paths.Material.matMoonBoulder;
            }

            if (SceneManager.GetActiveScene().name == "meridian") {
                position = new Vector3(125, 26.1f, -23);
                euler = new Vector3(0, 315, 0);
                boardPosition = new Vector3(125.5f, 25.1f, -22.3f);
                boardRotation = new Vector3(270, 35, 0);
                mat = Paths.Material.matPMTerrainPlayZoneWall;
            }

            if (SceneManager.GetActiveScene().name == "voidstage") {
                position = new Vector3(-37, -5.9f, 14f);
                euler = new Vector3(0, 270, 0);
                boardPosition = new Vector3(-37, -7.35f, 14);
                boardRotation = new Vector3(270, 0, 0);
                mat = Paths.Material.matVoidCoralPlatformMagenta;
            }

            if (position != Vector3.zero && Run.instance.IsExpansionEnabled(Paths.ExpansionDef.DLC3)) {
                if (NetworkServer.active) {
                    var obj = GameObject.Instantiate(Paths.GameObject.MealPrep, position, Quaternion.Euler(euler));
                    NetworkServer.Spawn(obj);
                }

                var board = GameObject.Instantiate(Paths.GameObject.BazaarLunarTable, boardPosition, Quaternion.Euler(boardRotation));
                board.transform.localScale = Vector3.one * 0.889f;
                board.transform.position = boardPosition;
                board.transform.rotation = Quaternion.Euler(boardRotation);
                board.GetComponent<MeshRenderer>().material = mat;
                var s1 = GameObject.Instantiate(Paths.GameObject.DisplaySteakFlat, board.transform);
                s1.transform.localPosition = new Vector3(-1.5f, -1.35f, 1.3f);
                s1.transform.localRotation = Quaternion.Euler(0, 0, 0);
                var s2 = GameObject.Instantiate(Paths.GameObject.DisplaySteakFlat, board.transform);
                s2.transform.localPosition = new Vector3(-2, -0.3f, 1.35f);
                s2.transform.localRotation = Quaternion.Euler(0, 0, 295);
                var s3 = GameObject.Instantiate(Paths.GameObject.DisplaySteakFlat, board.transform);
                s3.transform.localPosition = new Vector3(-1.8f, -0.3f, 1.5f);
                s3.transform.localRotation = Quaternion.Euler(0, 0, 70);
            }
        }
    }
}