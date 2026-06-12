using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Google.Protobuf;
using Cysharp.Net.Http;
using System.Net.Http;

public class xg : MonoBehaviour {

    public static xg instance;

    private string grpcHost = "http://localhost:8999";

    private GrpcChannel channel;
    private XGBoostRegression.XGBoostRegressionClient xgboostClient;

    void Awake() {
        instance = this;
    }

    void Start() {

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var handler = new YetAnotherHttpHandler();
        handler.SkipCertificateVerification = true;
        handler.Http2Only = true;

        var options = new GrpcChannelOptions();
        options.HttpHandler = handler;
        options.Credentials = ChannelCredentials.Insecure;
        
        channel = GrpcChannel.ForAddress(grpcHost, options);
        xgboostClient = new XGBoostRegression.XGBoostRegressionClient(channel);
    }
    public struct XgPrediction {
        public float GrowthCm;
        public float LeafCount;
        public float NodeCm;
    }


    // Create and fit a regressor.
   public XgPrediction Predict(XGBoostRequest req) {
        var response = xgboostClient.Predict(req);

        XgPrediction pred;
        pred.GrowthCm  = response.GrowthCm;
        pred.LeafCount = response.LeafCount;
        pred.NodeCm = response.NodeCm;
        return pred;
    }
}
