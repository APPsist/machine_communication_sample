using Appsist.Entities;
using Appsist.Rest;
using Appsist.Rest.Discovery;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net.Sockets;

namespace Demonstrator
{
    class Program
    {
        //Vergebene Adressen:
        //192.168.1.20 Roboter     
        //192.168.1.99 MES-Laptop (WIN)    
        //192.168.1.80 APPsist-System(extern) 

        static string ip_robot = "192.168.1.20"; 
        static int port_robot  =  9999;
        static string ip_app   = "192.168.1.99"; 
        static int port_app    =  8095;

        static void Main(string[] args)
        {
            while (true)  //continously try to connect. (in case of servered connection) 
            {       
                Console.WriteLine("INITIALIZE ... ");
                clnt c = new clnt(ip_robot, port_robot, ip_app , port_app);

                Console.WriteLine("CONNECT ... ");
                c.connect();

                Console.WriteLine(" ");
                Console.WriteLine("LISTEN ... ");
                c.listen_for_error_and_state();

                Console.WriteLine(" ");
                Console.WriteLine("DISCONNECT ... ");
                c.disconnect();

                Console.WriteLine("DRIVER STOPPED! ");
                Console.WriteLine(" ");
                Console.WriteLine(" ");
                Console.WriteLine(" ");
                Console.WriteLine("[//////////////////////////////////////////////////////////////////]");
                Console.WriteLine(" ");
                Console.WriteLine("Trying again ... ");
                Console.WriteLine(" ");

            }
        }
    }


    //-------------------------------------------------------------------------


    public class clnt
    {
        bool first_msg;
        bool robot_state_bool_old = true;
        bool q1_old = true;
        bool q2_old = true;
        bool teil_old = true;

        string ip_robot;
        int  port_robot;
        string ip_app;
        int  port_app;

        TcpClient tcpclnt;
        const int max_msg_length = 1024; //1024
        const int wait_ms = 250;  //1250;

        Service service1;
        AppsistRestClient client1;
        MachineSchema fmachineSchema;

        //Fixed commands for interaction wit robot controller
        static private string cmd_init = "1;1;OPEN=NARCUSR";
        static private string cmd_query_out = "1;1;OUT0";
        static private string cmd_query_err = "1;1;ERROR"; //Qok<Fehlernummer>;<error level> 


        public clnt(string _ip_robot, int _port_robot, string _ip_app, int _port_app)
        {
            
            //Console.WriteLine("Constructor ... ");
            first_msg = true;

            ip_robot = _ip_robot;
            port_robot = _port_robot;
            ip_app = _ip_app;
            port_app = _port_app;

            tcpclnt = new TcpClient();
            Console.WriteLine("--------------------------------------------------------------------");
            Console.WriteLine("Robot parameters:");
            Console.WriteLine("    IP:   " + ip_robot);
            Console.WriteLine("    Port: " + port_robot);
            Console.WriteLine(" ");

            
            String APPaddr = @"http://192.168.1.99:8095/services/mid/";

            Console.WriteLine("APPsist parameters:");
            Console.WriteLine("    IP:   " + ip_app);
            Console.WriteLine("    Port: " + port_app);
            Console.WriteLine("    Full address: " + APPaddr);
            Console.WriteLine(" ");
            Console.WriteLine("HACK IN PLACE FOR ADRESS!!! ");
            Console.WriteLine("--------------------------------------------------------------------");
            Console.WriteLine(" ");

            //MID
            service1 = new Service(APPaddr);
            //service1 = new Service("http", "localhost", 8095);
            //service1 = new Service("http", "192.168.1.99", 8095);
            //service1 = new Service(@"http://192.168.1.99:8095/services/mid/"); //HACK to achieve correct address

            client1 = new AppsistRestClient(service1);
            

            // schema message
            SchemaMessage schemaMessage = new SchemaMessage();
            Machine machine = new Machine("1", "20", "RV-2FB Robot Arm Controller");

            fmachineSchema = new MachineSchema(machine, "1", "1");

            fmachineSchema.AddSpecification(new MachineValueSpecification("state_ok",           MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.NEVER));
            fmachineSchema.AddSpecification(new MachineValueSpecification("q1",                 MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.NEVER));
            fmachineSchema.AddSpecification(new MachineValueSpecification("q2",                 MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.NEVER));
            fmachineSchema.AddSpecification(new MachineValueSpecification("Tuer offen",         MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.OVERVIEW));
            fmachineSchema.AddSpecification(new MachineValueSpecification("Federmagazin leer",  MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.OVERVIEW));
            fmachineSchema.AddSpecification(new MachineValueSpecification("Deckelmagazin leer", MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.OVERVIEW));
            fmachineSchema.AddSpecification(new MachineValueSpecification("Teil verloren",      MachineValueType.BOOL, Unit.NONE, VisualizationType.ON_OFF_LIGHT, VisualizationLevel.OVERVIEW));

            schemaMessage.Add(fmachineSchema);
            Console.WriteLine("Trying to send schema message: ");
            Console.WriteLine("(Requires running APPsist-System)");
            Console.WriteLine(schemaMessage);
//            client1.Send(schemaMessage);
            
            //Console.WriteLine("schema message sent ... ");
        }

        public void connect()
        {
            string res = "";

            try
            {
                tcpclnt.Connect(ip_robot, port_robot);
                Console.WriteLine("CONNECTED--------------------------");

                res = query(cmd_init);
                Console.WriteLine(res);
                Console.WriteLine("INITIALIZED-------------------------");
            }

            catch (Exception e)
            {
                Console.WriteLine("Error at connecting .....\n" + e.StackTrace);
            }
        }


        public void listen_for_error_and_state()
        {
            string res_sta = "";
            string res_err = "";
            try
            {
                while (true)
                {
                    String timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); 
                    String str = cmd_query_out;
                    //Console.WriteLine("Transmitting for query: " + str);

                    Console.WriteLine(timestamp + " -------------------------------------------------");

                    res_sta = query(cmd_query_out);
                    Console.WriteLine(res_sta);

                    //ID of bit in state bytes
                    int q1b = 2;
                    int q2b = 3;
                    int teilb = 7;

                    string rel = res_sta.Substring(5, 2); //Takes last 2 places of response
                    Console.WriteLine(rel);
                    byte r = byte.Parse(rel, System.Globalization.NumberStyles.HexNumber);
                    //Console.WriteLine("Zustandbytes:");
                    //Console.WriteLine(r);

                    bool q1 = (r & (1 << q1b)) != 0;
                    bool q2 = (r & (1 << q2b)) != 0;
                    bool teil = (r & (1 << teilb)) != 0;

                    Console.WriteLine("q1:   " + q1);
                    Console.WriteLine("q2:   " + q2);
                    Console.WriteLine("teil: " + teil);

                    if (q1 && q2 && (teil== false))
                    {
                        Console.WriteLine("KOLBEN NACHFUELLEN");
                        q1 = false;
                        q2 = false;
                    }

                    if (q1 && q2 && teil)
                    {
                        Console.WriteLine("TEIL VERLOREN");
                        q1 = false;
                        q2 = false;
                    }

                    if (q1==false && q2 && teil==false)
                    {
                        Console.WriteLine("DECKEL NACHFUELLEN");
                    }

                    if (q1  && q2 == false && teil == false)
                    {
                        Console.WriteLine("FEDERN NACHFUELLEN");
                    }



                    Console.Write("... \n");//---------------------------------
                    res_err = query(cmd_query_err);
                    Console.WriteLine(res_err);

                    //char errorstate_c = res[2];
                    bool robot_state_bool = res_err.Contains('K');
                    string robot_state_str = "FEHLER";
                    if (robot_state_bool)
                        robot_state_str = "OK";
                    Console.WriteLine("Zustand: " + robot_state_str);

                    Console.WriteLine("");

                    // Send data to MID ------------------------------------------
                    MachineData fmachineData = new MachineData(fmachineSchema);

                    fmachineData.Put("state_ok", robot_state_bool);
                    fmachineData.Put("q1", q1);
                    fmachineData.Put("q2", q2);
                    fmachineData.Put("Tuer offen", !robot_state_bool);
                    fmachineData.Put("Federmagazin leer", q1);
                    fmachineData.Put("Deckelmagazin leer", q2);
                    fmachineData.Put("Teil verloren", teil);


                    if (first_msg || (q1 != q1_old) || (q2 != q2_old) || (teil != teil_old) || (robot_state_bool != robot_state_bool_old))
                    {
                        DataMessage dataMessage1 = new DataMessage();
                        dataMessage1.Add(fmachineData);
                        Console.WriteLine("Sending Data Message: " + dataMessage1);

//                        client1.Send(dataMessage1);
                    }

                    q1_old = q1;
                    q2_old = q2;
                    teil_old = teil;
                    robot_state_bool_old = robot_state_bool;
                    first_msg = false;


                    Thread.Sleep(wait_ms);
                }//end while
            }
            catch (Exception e)
            {
                Console.WriteLine("Error at listening for state.....\n" + e.StackTrace);
            }
        }

        private string query(string str)
        {
            string res = "";
            try
            {
                Stream stm = tcpclnt.GetStream();

                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(str);

                stm.Write(ba, 0, ba.Length);

                byte[] bb = new byte[max_msg_length];
                int k = stm.Read(bb, 0, max_msg_length);
                char[] cc = new char[k];

                for (int i = 0; i < k; i++)
                {
                    cc[i] = Convert.ToChar(bb[i]);
                }

                res = new string(cc);
            }

            catch (Exception e)
            {
                Console.WriteLine("Error at query for state.....\n" + e.StackTrace);
            }

            return res;
        }


        public void disconnect()
        {
            try
            {
                tcpclnt.Close();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error at disconnecting.....\n " + e.StackTrace);
            }
        }
    }
}

