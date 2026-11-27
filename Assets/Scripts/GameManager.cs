using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

[System.Serializable]
public class GameData
{
	public int curlvl;
	public bool[] bought;
	public bool[] has_link;
	public bool[] has_crystal;
	public bool has_bigcash;
	public int links;
	public int crystals;
	public int cash;
	public int shirt;
	public int kasa;
}

public class GameManager : MonoBehaviour {
		
	public GameObject paauseThings;
		
	public bool mainMenu = false;
	private bool inLayoutMenu = false;
	
	public static int bld_platform = 0; //0 - PC, 1 - N3DS, 2 - Vita, 3 - Android, 4 - NX, 5 - X360, 6 - PS4
	public static int bld_lnge = 0; // 0 - english, 1 - russian
	public static bool paused = false;
	public static float music_vol = .4f;
	
	public static int curlvl = 0;

	public int currentKeys;
	public Text keyText;
	public int currentLinks;
	public Text linksText;
	public int burgers;
	public int totalBurgers;
	public Text burgersText;
	public GameObject chainBox;
	public int kidsters;
	
	//pause text
	public Text psd_paused;
	public Text psd_contin;
	public Text psd_musvol;
	public Text psd_langug;
	public Text psd_exitlv;
	public Text psd_exitgm;
	public Text psd_crystl;
	public Text psd_invert;
	
	//shop text
	public Text shop_title;
	public List<Text> shop_price;
	public List<Text> shop_buy;
	public List<GameObject> shop_button;
	public List<GameObject> shop_button_dress;
	public static bool[] bought = {true,false,false,false,false,false};
	public static int[] sh_price = {100,1100,1100,1100,1100,1100};
	
	//mainmenu text
	public Text mm_new;
	public Text mm_con;
	public Text mm_lay;
	public Text mm_ext;
	public Text mm_lng;
	public Text mm_bck;
	public List<Text> mm_layout0;
	public List<Text> mm_layout1;
	public List<Text> mm_layout2;
	public List<GameObject> layout_obj;
	public GameObject mm_menu0;
	public GameObject mm_menu1;
	public GameObject contButton;
	
	string[] lays_eng = {"Pause", "Dash", "Movement", "Jump", "Attack", "Hook", "Camera"};
	string[] lays_rus = {"Пауза", "Рывок", "Движение", "Прыжок", "Удар", "Крюк", "Камера"};

	public AudioSource source_pickedup;
	public AudioClip clip_pickedup;
	public AudioClip clip_pickedcash;
	public AudioClip clip_paused;
	public AudioSource musicSource;

	public GameObject spoo;
	
	public static bool[] has_link = {false,false,false,false,false,false};
	public static bool[] has_crystal = {false,false,false,false,false,false,false,false,false,false};
	public static bool allDone = false;
	
	public static int links = 0;
	public static int crystals = 0;
	public static int cash = 0;
	public static int shirt = 0;
	public static int kasa = 0;
	public static bool has_bigcash = false;
	public Text cashText;
	
	private int cashShow = 0;
	private bool refreshCashShow = false;
	
	public GameObject crystalBridge;
	
	public Animator anim;
	public Animator playerAnim;
	
	private Resolution currentResolution;
	
	public static bool isLevelChanging = false;

	/*void Awake () {
		if (!Instance)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else{
			Destroy(gameObject);
		}
	}*/

	// Use this for initialization
	void Start () {
				
		loadPrefs();
		
		isLevelChanging = false;
		
		string path = Application.persistentDataPath + "/savegame.dat";
        if (!File.Exists(path))
        {
			Destroy (contButton);
		}
		
		//playerAnim = GetComponent<Animator>();
		musicSource.volume = music_vol;
		
		// Получаем текущее разрешение экрана
		currentResolution = Screen.currentResolution;
		
		// Устанавливаем разрешение экрана в fullscreen = true
		//Screen.SetResolution(currentResolution.width, currentResolution.height, true);
		
		if(SceneManager.GetActiveScene().buildIndex==1){
			refreshShop();
		}
		
		if(!mainMenu){
			changePauseLanguage();
			//RefreshCash();
			//refreshCrystals();
		}
		
		if(mainMenu){
			keepLayoutByPlatform();
			revertMenu();
		}
		
		cashShow = cashShow;
		
		if( bld_platform == 1 ){
			paauseThings.transform.localScale = new Vector3 (1.4f, 1.4f, 1.4f);
		}
		
		Debug.Log("sht: " + shirt);
		
	}
	
	// Update is called once per frame
	void Update () {
		
		//Debug.Log(SceneManager.GetActiveScene().buildIndex+" "+crystals);
		if( SceneManager.GetActiveScene().buildIndex == 1 ){
			if(crystals >= 10){
				crystalBridge.transform.localScale = new Vector3 (1f, 1f, 1f);
			}
			else{
				crystalBridge.transform.localScale = new Vector3 (0f, 0f, 0f);
			}
		}
		
		
		
		
		if( cashShow != cash ){
			cashShow = Mathf.RoundToInt(Mathf.Lerp(cashShow, cash, Time.deltaTime * 10));
			if( cashShow >= cash - 2){
				cashShow = cash;
			}
			refreshCashShow = true;
		}
		
		//if ( refreshCashShow ) RefreshCash();
		
	}
	
	/*void OnApplicationFocus(bool hasFocus)
	{
		if(!hasFocus) 
		{
			// Получаем текущее разрешение экрана
			Resolution currentResolution = Screen.currentResolution;
			
			// Устанавливаем разрешение экрана в fullscreen = false
			Screen.SetResolution(currentResolution.width, currentResolution.height, false);
		}
	}*/

	public void exitApp(){
		Application.Quit();
	}

	public void toggleFullScreen(){
		// Toggle fullscreen
        Screen.fullScreen = !Screen.fullScreen;
		/*if(Screen.fullScreen){
			Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
		}
		else{
			Screen.SetResolution(currentResolution.width, currentResolution.height, true);
		}*/
	}
	
	public void lvlChanging(){
		isLevelChanging = true;
	}

	public void doPause(){
		paused = !paused;
		Cursor.visible = paused;
		//anim.SetBool("paused",paused);
		//source_pickedup.PlayOneShot(clip_paused);
		if(paused){
			//playerAnim.CrossFade(playerAnim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0f);
			//refreshCrystals();
			//playerAnim.speed = 0f;
			//Cursor.lockState = CursorLockMode.None;
			//if(bld_platform!=2){saveGame(curlvl);}
		}
		else{
			/*playerAnim.speed = 1f;
			Cursor.lockState = CursorLockMode.Locked;
			savePrefs();*/
		}
		//EventSystem eventSystem = FindObjectOfType<EventSystem>();
		EventSystem.current.SetSelectedGameObject(null);
	}
	
	public void langEng(){
		changeLanguage(0);
	}
	
	public void langRus(){
		changeLanguage(1);
	}
	
	public void changeLanguage(int lang){
		bld_lnge = lang;
		if(mainMenu){
			refreshMainMenuText();
		}
		changePauseLanguage();
	}
	
	public void changePauseLanguage(){
		if(bld_lnge==0 && !mainMenu){
			psd_paused.text="PAUSED";
			psd_contin.text="Continue";
			psd_musvol.text="Music vol.";
			psd_langug.text="Language";
			psd_exitlv.text="Exit level";
			psd_exitgm.text="Exit game";
			psd_invert.text="Invert Y";
			
			//cashText.text = "Cash: " + cash;
			//linksText.text = "Links: " + currentLinks + " / 3";
			//burgersText.text = "Artefacts: " + burgers;
		}
		else if(bld_lnge==1 && !mainMenu){
			psd_paused.text="ПАУЗА";
			psd_contin.text="Продолжить";
			psd_musvol.text="Громкость муз.";
			psd_langug.text="Язык";
			psd_exitlv.text="Покинуть уровень";
			psd_exitgm.text="Выйти из игры";
			psd_invert.text="Обратить Y";
			
			//cashText.text = "Налик: " + cash;
			//linksText.text = "Звенья: " + currentLinks + " / 3";
			//burgersText.text = "Артефакты: " + burgers;
		}
	}
	
	public void refreshShop(){
		if(bld_lnge==0 && !mainMenu){
			shop_title.text="SHOP";
			for(int i=0 ; i<shop_price.Count ; i++){
				if(bought[i]){
					shop_price[i].text="Bought";
					shop_button[i].SetActive(false);
				}
				else{
					shop_buy[i].text="Buy";
					shop_price[i].text="$"+sh_price[i];
				}
			}
		}
		if(bld_lnge==1 && !mainMenu){
			shop_title.text="МАГАЗИН";
			for(int i=0 ; i<shop_price.Count ; i++){
				if(bought[i]){
					shop_price[i].text="Есть";
					shop_button[i].SetActive(false);
				}
				else{
					shop_buy[i].text="Купить";
					shop_price[i].text="$"+sh_price[i];
				}
			}
		}
	}
	
	public void doShop(){
	}
	
	public void undoShop(){
	}
	
	public void AddCrystal(int id){
		has_crystal[id]=true;
		crystals++;
		refreshCrystals();
	}
	
	public void refreshCrystals(){
		psd_crystl.text = crystals + " / 10";
	}
	
	
	
	private void refreshMainMenuText(){
		if(bld_lnge==0 && mainMenu){
			mm_new.text="New Game";
			mm_con.text="Continue";
			mm_lay.text="Layout";
			mm_lng.text="Language";
			mm_ext.text="Exit Game";
			mm_bck.text="Back";
			for(int i=0 ; i<mm_layout0.Count ; i++){
				mm_layout0[i].text=lays_eng[i];
				mm_layout1[i].text=lays_eng[i];
				mm_layout2[i].text=lays_eng[i];
			}
		}
		else if(bld_lnge==1 && mainMenu){
			mm_new.text="Новая игра";
			mm_con.text="Продолжить";
			mm_lay.text="Управление";
			mm_lng.text="Язык";
			mm_ext.text="Выйти из игры";
			mm_bck.text="Назад";
			for(int i=0 ; i<mm_layout0.Count ; i++){
				mm_layout0[i].text=lays_rus[i];
				mm_layout1[i].text=lays_rus[i];
				mm_layout2[i].text=lays_rus[i];
			}
		}
	}
	
	private void keepLayoutByPlatform(){
		for(int i=0 ; i<layout_obj.Count ; i++){
			if(i!=bld_platform){
				layout_obj[i].SetActive(false);
			}
		}
	}
	
	public void revertLayoutMenuButton(){
		inLayoutMenu = !inLayoutMenu;
		revertMenu();
	}
	
	private void revertMenu(){
		if(inLayoutMenu){
			mm_menu0.SetActive(false);
			mm_menu1.SetActive(true);
		}
		else{
			mm_menu0.SetActive(true);
			mm_menu1.SetActive(false);
		}
	}
	
	private void savePrefs(){
		PlayerPrefs.SetInt("bld_lnge", bld_lnge);
		PlayerPrefs.SetFloat("music_vol", music_vol);
		PlayerPrefs.Save();
	}
	
	public void loadPrefs(){
		if (PlayerPrefs.HasKey("SavedInteger"))
		{
			bld_lnge = PlayerPrefs.GetInt("bld_lnge");
			music_vol = PlayerPrefs.GetFloat("music_vol");
		}
	}
	
	public void resetPrefs(){
		PlayerPrefs.DeleteAll();
		bld_lnge = 0; // 0 - english, 1 - russian
		music_vol = .4f;
	}
	
	public void saveGame(int nextLvl){
		curlvl = nextLvl;
		
		GameData data = new GameData();
		data.curlvl = curlvl;
		data.bought = bought;
		data.has_link = has_link;
		data.has_crystal = has_crystal;
		data.has_bigcash = has_bigcash;
		
		data.links = links;
		data.crystals = crystals;
		data.cash = cash;
		data.shirt = shirt;
		data.kasa = kasa;
		
		BinaryFormatter formatter = new BinaryFormatter();
        string path = Application.persistentDataPath + "/savegame.dat";
        FileStream stream = new FileStream(path, FileMode.Create);

        formatter.Serialize(stream, data);
        stream.Close();
	}	

	public void loadGame(){
		string path = Application.persistentDataPath + "/savegame.dat";
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);

            GameData data = formatter.Deserialize(stream) as GameData;
            stream.Close();

            // Загрузка сохраненных данных в GameManager
            curlvl = data.curlvl;
			bought = data.bought;
			has_link = data.has_link;
			has_crystal = data.has_crystal;
			has_bigcash = data.has_bigcash;

			links = data.links;
			crystals = data.crystals;
			cash = data.cash;
			shirt = data.shirt;
			kasa = data.kasa;
        }
	}
	
	public void StartNewGame()
	{
		// Создание нового экземпляра данных игры
		GameData newData = new GameData();

		// Инициализация значений по умолчанию
		newData.curlvl = 1;
		newData.bought = new bool[]{true,false,false,false,false,false};
		newData.has_link = new bool[]{false,false,false,false,false,false};
		newData.has_crystal = new bool[]{false,false,false,false,false,false,false,false,false,false};

		newData.links = 0;
		newData.crystals = 0;
		newData.cash = 0;
		if(bld_platform!=2){newData.shirt = 0;}
		newData.kasa = 0;

		// Сохранение новых данных игры
		if(bld_platform!=2){
			SaveGameDataNew(newData);
		}
	}

	public void SaveGameDataNew(GameData data)
	{
		BinaryFormatter formatter = new BinaryFormatter();
		string path = Application.persistentDataPath + "/savegame.dat";
		FileStream stream = new FileStream(path, FileMode.Create);

		formatter.Serialize(stream, data);
		stream.Close();
	}
	
	/*public void change_shirt(int sht)
	{
		shirt = sht;
	}*/
	
}
