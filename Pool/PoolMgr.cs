using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

/// <summary>
/// 抽屉（池子中的数据）对象（自定义栈）
/// </summary>
public class PoolData
{
    //栈，储存池中现有对象
    private Stack<GameObject> dataStack = new Stack<GameObject>();

    //list，记录使用中的对象的 
    private List<GameObject> usedList = new List<GameObject>();

    //场景上同时存在的对象的上限个数（由PoolObj中的数值指定）
    private int maxNum;

    //根对象 用来进行布局管理的对象
    private GameObject rootObj;

    //获取容器中是否有对象
    public int Count => dataStack.Count;

    public int UsedCount => usedList.Count;

    /// <summary>
    /// 进行使用中对象数量和最大容量进行比较 小于返回true 需要实例化
    /// </summary>
    public bool NeedCreate => usedList.Count < maxNum;

    /// <summary>
    /// 初始化构造函数
    /// </summary>
    /// <param name="root">柜子（缓存池）父对象</param>
    /// <param name="name">抽屉父对象的名字</param>
    public PoolData(GameObject root, string name, GameObject usedObj)
    {
        //开启功能时 才会动态创建 建立父子关系
        if(PoolMgr.isOpenLayout)
        {
            //创建抽屉父对象
            rootObj = new GameObject(name);
            //和柜子父对象建立父子关系
            rootObj.transform.SetParent(root.transform);
        }

        //创建抽屉时 外部也会同时动态创建一个对象
        //将其记录到 使用中的对象容器中
        PushUsedList(usedObj);

        PoolObj poolObj = usedObj.GetComponent<PoolObj>();
        if (poolObj == null)
        {
            Debug.LogError("请为使用缓存池功能的预设体对象挂载PoolObj脚本 用于设置数量上限");
            return;
        }
        //记录上限数量值
        maxNum = poolObj.maxNum;
    }

    /// <summary>
    /// 从抽屉中弹出数据对象
    /// </summary>
    /// <returns>想要的对象数据</returns>
    public GameObject Pop()
    {
        //取出对象
        GameObject obj;

        if (Count > 0)
        {
            //从空闲容器（栈）当中取出使用
            obj = dataStack.Pop();
            //用使用中的容器（list）记录
            usedList.Add(obj);
        }
        else
        {
            //取0索引的对象 代表的就是使用时间最长的对象
            obj = usedList[0];
            //把它从使用着的对象中移除
            usedList.RemoveAt(0);
            usedList.Add(obj);
        }

        //激活对象
        obj.SetActive(true);
        //断开父子关系（显示在空物体栏外）
        if (PoolMgr.isOpenLayout)
            obj.transform.SetParent(null);

        return obj;
    }

    /// <summary>
    /// 将物体放入到抽屉对象中
    /// </summary>
    /// <param name="obj"></param>
    public void Push(GameObject obj)
    {
        //失活放入抽屉的对象
        obj.SetActive(false);
        //放入对应抽屉的根物体中 建立父子关系
        if (PoolMgr.isOpenLayout)
            obj.transform.SetParent(rootObj.transform);
        //通过栈记录对应的对象数据
        dataStack.Push(obj);
        //这个对象已经不再使用了 应该把它从记录容器中移除
        usedList.Remove(obj);
    }


    /// <summary>
    /// 将对象压入到使用中的容器中记录
    /// </summary>
    /// <param name="obj"></param>
    public void PushUsedList(GameObject obj)
    {
        usedList.Add(obj);
    }
}

/// <summary>
/// 方便在字典当中用里式替换原则 存储子类对象
/// </summary>
public abstract class PoolObjectBase { }

/// <summary>
/// 用于存储 数据结构类 和 逻辑类 （不继承mono的）容器类
/// </summary>
/// <typeparam name="T"></typeparam>
public class PoolObject<T> : PoolObjectBase where T:class
{
    public Queue<T> poolObjs = new Queue<T>();
}

/// <summary>
/// 要被复用的 数据结构类、逻辑类 都必须要继承该接口
/// </summary>
public interface IPoolObject
{
    /// <summary>
    /// 重置数据的方法
    /// </summary>
    void ResetInfo();
}

/// <summary>
/// 缓存池(对象池)模块 管理器
/// </summary>
public class PoolMgr : BaseManager<PoolMgr>
{
    //柜子容器当中有抽屉的体现
    //值 其实代表的就是一个 抽屉对象
    private Dictionary<string, PoolData> poolDic = new Dictionary<string, PoolData>();

    /// <summary>
    /// 用于存储数据结构类、逻辑类对象的 池子的字典容器
    /// </summary>
    private Dictionary<string, PoolObjectBase> poolObjectDic = new Dictionary<string, PoolObjectBase>();

    //池子根对象
    private GameObject poolObj;

    //是否开启布局功能
    public static bool isOpenLayout = false;

    private PoolMgr() {
    #if UNITY_EDITOR
        isOpenLayout = true;
    #endif
        //如果根物体为空 就创建
        if (poolObj == null && isOpenLayout)
            poolObj = new GameObject("Pool");

    }

    /// <summary>
    /// 拿东西的方法
    /// </summary>
    /// <param name="name">抽屉容器的名字</param>
    /// <returns>从缓存池中取出的对象</returns>
    public GameObject GetObj(string name)
    {
        //如果根物体为空 就创建
        if (poolObj == null && isOpenLayout)
            poolObj = new GameObject("Pool");

        GameObject obj;
        if(!poolDic.ContainsKey(name) ||
            (poolDic[name].Count == 0 && poolDic[name].NeedCreate))
        {
            //动态创建对象
            obj = GameObject.Instantiate(Resources.Load<GameObject>(name));
            obj.name = name;

            //创建抽屉
            if(!poolDic.ContainsKey(name))
                poolDic.Add(name, new PoolData(poolObj, name, obj));
            else//记录实例化出的对象到使用中的对象容器中
                poolDic[name].PushUsedList(obj);
        }
        else
        {
            obj = poolDic[name].Pop();
        }
        return obj;
    }
    /// <summary>
    /// 传入一个预制体并取出
    /// </summary>
    /// <param name="gobj">预制体</param>
    /// <returns></returns>
    public GameObject GetObj(GameObject gobj)
    {
        //如果根物体为空 创建
        if (poolObj == null && isOpenLayout)
            poolObj = new GameObject("Pool");

        GameObject obj;
        if (!poolDic.ContainsKey(gobj.name) ||
            (poolDic[gobj.name].Count == 0 && poolDic[gobj.name].NeedCreate))
        {
            //动态创建对象
            obj = GameObject.Instantiate(gobj);
            obj.name = gobj.name;

            //创建抽屉
            if (!poolDic.ContainsKey(gobj.name))
                poolDic.Add(gobj.name, new PoolData(poolObj, gobj.name, obj));
            else//记录实例化出的对象到使用中的对象容器中
                poolDic[gobj.name].PushUsedList(obj);
        }
        else
        {
            obj = poolDic[gobj.name].Pop();
        }
        return obj;
    }
    /// <summary>
    /// 拿东西的方法 路径重载
    /// </summary>
    /// <param name="name">物品名称</param>
    /// <param name="path">物品位于Resoures下的路径</param>
    /// <returns></returns>
    public GameObject GetObj(string name,string path)
    {
        //如果根物体为空 就创建
        if (poolObj == null && isOpenLayout)
            poolObj = new GameObject("Pool");

        GameObject obj;
        if (!poolDic.ContainsKey(name) ||
            (poolDic[name].Count == 0 && poolDic[name].NeedCreate))
        {
            obj = GameObject.Instantiate(Resources.Load<GameObject>(path));
            obj.name = name;
            if (!poolDic.ContainsKey(name))
                poolDic.Add(name, new PoolData(poolObj, name, obj));
            else
                poolDic[name].PushUsedList(obj);
        }
        else
        {
            obj = poolDic[name].Pop();
        }
        return obj;
    }

    /// <summary>
    /// 获取自定义的数据结构类和逻辑类对象 （不继承Mono的）
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <returns></returns>
    public T GetObj<T>(string nameSpace = "") where T:class,IPoolObject,new()
    {
        //池子的名字 是根据类的类型来决定的 就是它的类名
        string poolName = nameSpace + "_" + typeof(T).Name;
        //有池子
        if(poolObjectDic.ContainsKey(poolName))
        {
            PoolObject<T> pool = poolObjectDic[poolName] as PoolObject<T>;
            //池子当中是否有可以复用的内容
            if(pool.poolObjs.Count > 0)
            {
                //从队列中取出对象 进行复用
                T obj = pool.poolObjs.Dequeue() as T;
                return obj;
            }
            //池子当中是空的
            else
            {
                //必须保证存在无参构造函数
                T obj = new T();
                return obj;
            }
        }
        else//没有池子
        {
            T obj = new T();
            return obj;
        }
        
    }

    /// <summary>
    /// 往缓存池中放入对象
    /// </summary>
    /// <param name="name">抽屉（对象）的名字</param>
    /// <param name="obj">希望放入的对象</param>
    public void PushObj(GameObject obj)
    {
        //若对应物体不存在
        if (!poolDic.ContainsKey(obj.name))
            poolDic.Add(obj.name, new PoolData(poolObj, obj.name, obj));
        //往抽屉当中放对象
        poolDic[obj.name].Push(obj);
    }

    /// <summary>
    /// 将自定义数据结构类和逻辑类 放入池子中
    /// </summary>
    /// <typeparam name="T">对应类型</typeparam>
    public void PushObj<T>(T obj, string nameSpace = "") where T:class,IPoolObject
    {
        //如果想要压入null对象 是不被允许的
        if (obj == null)
            return;
        //池子的名字 是根据类的类型来决定的 就是它的类名
        string poolName = nameSpace + "_" + typeof(T).Name;
        //有池子
        PoolObject<T> pool;
        if (poolObjectDic.ContainsKey(poolName))
            //取出池子 压入对象
            pool = poolObjectDic[poolName] as PoolObject<T>;
        else//没有池子
        {
            pool = new PoolObject<T>();
            poolObjectDic.Add(poolName, pool);
        }
        //在放入池子中之前 先重置对象的数据
        obj.ResetInfo();
        pool.poolObjs.Enqueue(obj);
    }

    /// <summary>
    /// 用于清除整个柜子当中的数据 
    /// 使用场景 主要是 切场景时
    /// </summary>
    public void ClearPool()
    {
        poolDic.Clear();
        poolObj = null;
        poolObjectDic.Clear();
    }
}
