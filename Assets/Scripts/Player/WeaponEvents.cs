using UnityEngine;

public class WeaponEvents : MonoBehaviour
{
    [SerializeField] SwordEquipController sword;   // arraste sua ref aqui

    // Chamado no clipe de sacar, no frame em que a mão "pega" a arma
    public void ShowWeapon()
    {
        if (!sword) return;
        // prefira SetEquipped se você tiver; senão use Toggle(true)
        sword.SetEquipped(true);
    }

    // Chamado no clipe de guardar, no frame em que a arma "entra" na bainha
    public void HideWeapon()
    {
        if (!sword) return;
        sword.SetEquipped(false);
    }
}
