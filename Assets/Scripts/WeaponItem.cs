using UnityEngine;

public class WeaponItem : MonoBehaviour
{
    public WeaponData weaponToGive; // Kéo file WeaponData (ví dụ Kiếm xịn) vào đây

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Kiểm tra nếu là Player và đang ở Route Abyss (vì Abyss mới nuốt)
        if (other.CompareTag("Player") && GameManager.instance.currentRoute == GameManager.GameRoute.Abyss)
        {
            PlayerMovement player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                // Gọi hàm "nuốt" từ Player và truyền dữ liệu vũ khí này vào
                player.StartCoroutine(player.EvolveRoutine(weaponToGive));

                // Nuốt xong thì cái đồ dưới đất biến mất
                Destroy(gameObject);
            }
        }
    }
}