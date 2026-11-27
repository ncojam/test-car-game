using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Vector3 initialDirectionVector;
	public float speed;
	public float ySpeedInitial;
	private float ySpeed;
	public float weight;

    public void initialDirection(Vector3 direction)
    {
        initialDirectionVector = direction;
		ySpeed = ySpeedInitial;
    }

    void Update()
    {
		if(ySpeed > -100f){
			ySpeed -= Time.deltaTime * weight;
		}
        // Применяем гравитацию к позиции снаряда
		//float newYPosition = transform.position.y + ySpeed * Time.deltaTime; // Вычисляем новую позицию по оси Y

		// Двигаем снаряд по новой позиции
		//transform.position = new Vector3(transform.position.x, newYPosition, transform.position.z);

		// Движение снаряда в заданном направлении по остальным осям
		transform.Translate(new Vector3(initialDirectionVector.x, initialDirectionVector.y + ySpeed, initialDirectionVector.z) * Time.deltaTime * speed, Space.World);
    }
}
