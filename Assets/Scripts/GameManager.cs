using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameRoute { Undecided, Light, Abyss }
    public GameRoute currentRoute = GameRoute.Undecided;

    public static GameManager instance;

    void Awake()
    {
        // Khởi tạo Singleton
        if (instance == null) instance = this;
    }

    void Update()
    {
        // Nhấn phím 1 chọn Light, phím 2 chọn Abyss
        if (currentRoute == GameRoute.Undecided)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRoute(GameRoute.Light);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectRoute(GameRoute.Abyss);
        }
    }

    void SelectRoute(GameRoute route)
    {
        currentRoute = route;

        // Tìm Player trong cảnh
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();

        if (route == GameRoute.Light)
        {
            Camera.main.backgroundColor = new Color(0.9f, 0.8f, 0.5f); // Vàng
            if (player != null) player.ChangeWeaponByRoute(GameRoute.Light);
            UnityEngine.Debug.Log("Light Mode: Use this buddy!");
        }
        else
        {
            Camera.main.backgroundColor = new Color(0.2f, 0.1f, 0.3f); // Tím
            if (player != null) player.ChangeWeaponByRoute(GameRoute.Abyss);
            UnityEngine.Debug.Log("Abyss Mode: Take it!");
        }
    }
}