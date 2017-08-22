#include <Wire.h>  
#include <String.h>
#include <dht11.h>
#include <SPI.h>
#include <RFID.h>
#include <Servo.h> 

#define SLAVE_ADDRESS 0x40   // Define the i2c address
#define led1 30
#define led2 31
#define led3 32
#define led4 34
#define trigPin 9
#define echoPin 8
#define SDA_DIO 11
#define RESET_DIO 10
#define buzzer 12

Servo servom;  
dht11 DHT11;

const int Gaz_Pin = A2;  
const int LDR_Pin = A1; 
const int Su_Pin = A0;
int gaz,LDR,su,kapi_mesafe;
bool girisvarmi=true,kapi_acik=false;
String tempera,nem,sicaklik;
String kullanici="";
char mesaj[100];

RFID RC522(SDA_DIO, RESET_DIO); 

void setup()
{
  
 DHT11.attach(2);
 servom.attach(7);
 Serial.begin(9600);
 pinMode(trigPin, OUTPUT);
 pinMode(buzzer, OUTPUT);
 pinMode(echoPin, INPUT);
 pinMode(led1,OUTPUT);
 pinMode(led2,OUTPUT);
 pinMode(led3,OUTPUT);
 pinMode(led4,OUTPUT);
 SPI.begin(); 
 RC522.init();
 
 Wire.begin(SLAVE_ADDRESS); 

}

void loop()
{
   int chk = DHT11.read();
   sicaklik=(float)DHT11.temperature;
   nem=(float)DHT11.humidity;
   gaz=analogRead(Gaz_Pin);
   su=analogRead(Su_Pin);
   ldr_oku();
   kapi_mesafe=Mesafe_Olc();
   kullanici=RF_oku();
   bluetooth_led();
   alarm();
   tempera=sicaklik+";"+nem+";"+kapi_mesafe+";"+LDR+";"+gaz+";"+su+";"+kullanici;
   tempera.toCharArray(mesaj,100);

   Serial.println(mesaj);
   Wire.onRequest(sendData);
   delay(2000);
}

void ldr_oku(){
  LDR = analogRead(LDR_Pin);
  if(LDR<350){
    digitalWrite(led2,HIGH);
  }
  else{
    digitalWrite(led2,LOW);
  }
}

void bluetooth_led(){
if(Serial.available()) // Eğer Bluetooth bağlantısı varsa kodaları çalıştırır
  {
    char data = Serial.read();
    if(data=='1')
      digitalWrite(led1,HIGH);
    if(data=='2')
      digitalWrite(led1,LOW);
    if(data=='3')
      digitalWrite(led2,HIGH);
    if(data=='4')
      digitalWrite(led2,LOW);
    if(data=='5')
      digitalWrite(led3,HIGH);
    if(data=='6')
      digitalWrite(led3,LOW);
    if(data=='7')
      digitalWrite(led4,HIGH);
    if(data=='8')
      digitalWrite(led4,LOW);  
  }
}

void alarm(){
  if(gaz>500){
    digitalWrite(buzzer,HIGH);
    delay(250);
    digitalWrite(buzzer,LOW);
    digitalWrite(led1,LOW);
    digitalWrite(led2,LOW);
    digitalWrite(led3,LOW);
    digitalWrite(led4,LOW);
  }
  if(su>500){
    digitalWrite(buzzer,HIGH);
    delay(250);
    digitalWrite(buzzer,LOW);
    digitalWrite(led1,LOW);
    digitalWrite(led2,LOW);
    digitalWrite(led3,LOW);
    digitalWrite(led4,LOW);
  }
  if(kapi_mesafe<16 && girisvarmi){
    digitalWrite(buzzer,HIGH);
    delay(250);
    digitalWrite(buzzer,LOW);
  }
}

String RF_oku(){
    if (RC522.isCard())
    {
      if(kapi_acik) kapi_kapat();
      else kapi_ac();
      return "Bervan";
    }
    return "Null";
}

void kapi_ac(){
  int poz=0;
  girisvarmi=false;
  for(poz = 40; poz <= 180; poz += 20) // servomuzu 0dan 180 dereceye döndürüyoruz
  {
    servom.write(poz);              // servoya olması gereken pozisyon bilgisini gönderiyoruz
    delay(10);                       // her derecede beklemesi için
  }
  if(kapi_acik==false) kapi_acik=true;
}

void kapi_kapat(){
  int poz=0;
  for(poz = 180; poz>=40; poz-=20)     // 180den 0a geri döndürüyoruz
  {
    servom.write(poz);              // servoya olması gereken pozisyon bilgisini gönderiyoruz
    delay(10);                       // her derecede beklemesi için
  }
  girisvarmi=true;
  if(kapi_acik==true)kapi_acik=false;
}

long Mesafe_Olc(){
  long sure, mesafe;
  digitalWrite(trigPin, LOW);
  delayMicroseconds(2);
  digitalWrite(trigPin, HIGH);
  delayMicroseconds(10);
  digitalWrite(trigPin, LOW);
  sure = pulseIn(echoPin, HIGH);
  mesafe = (sure/2) / 29.1;
  return mesafe;
}

void sendData()
{
 Wire.write(mesaj); 
}
