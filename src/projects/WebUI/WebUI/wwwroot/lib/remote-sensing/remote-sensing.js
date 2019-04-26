var app = angular.module("fileUpload", ["xeditable", "ui.select", "ngMaterial", 'ngMessages', 'ngRoute', "ngFileUpload"]);

app.run(function (editableOptions) {
    editableOptions.theme = 'bs3';
});

app.config(function ($sceDelegateProvider) {
    $sceDelegateProvider.resourceUrlWhitelist([
        '**'
    ]);
});

app.controller('MyCtrl', ["$scope", "$filter", "$http", "$log", "$timeout", "$route", "$location", "Upload", function ($scope, $filter, $http, $log, $route, $location, $timeout, Upload) {

    $scope.status = { selectPrefix: false };
    $scope.selection = {
        row: 0, col: 0, tag: "image", operation: {}
    };
    $scope.metadata = {};

    $scope.getIframeSrc = function () {
        return "/yitong/index.html?siteUrl=" + $scope.current.cdn + "&service=" + $scope.current.prefix;
    };

    $scope.hasObject = function (obj) {
        if (obj == null) {
            return false;
        }
        for (var prop in obj) {
            if (obj.hasOwnProperty(prop))
                return true;
        }
        return false;
    };

    $scope.toggle = function (item, dic) {
        if (dic.hasOwnProperty(item)) {
            dic[item] = !dic[item];
        } else {
            dic[item] = true;
        };
    };

    $scope.status = function (item, dic) {
        if (dic.hasOwnProperty(item)) {
            return dic[item];
        } else {
            return false;
        }
    };

    $scope.clearResult = function () {
        delete $scope.errorMsg;
    };

    $scope.getCurrent = function (onCompletion) {
        $http.get('/api/Image/GetCurrent').then(function (response) {
            var current = response.data;
            $scope.current = response.data;
            $log.log($scope.current);
            if (onCompletion != null) {
                onCompletion();
            };
        });
    }

    $scope.onSuccess = function (tag, response, onCompletion) {
        if (response.data.error) {
            alert(tag + " Error: " + data.error);
        }
        $log.log(tag + " Success " + response.data);
        onCompletion(response.data);
    };

    $scope.onError = function (response) {
        alert('post request failed, check server?');
    }

    $scope.selectPrefixOld = function (onCompletion) {
        var post = $scope.current;
        var postInfo = $http.post('/api/Image/SelectPrefix', post);

        var successFunc = function (response) {
            $scope.onSuccess("selectPrefix", response, data => {
                $scope.selectedPrefix = $scope.current.prefix;
                $scope.prefixSelections = data.prefix;
                $scope.status.selectPrefix = true;
            });
            if (onCompletion != null) {
                onCompletion();
            };
        };

        postInfo.then(successFunc, $scope.onError);
    }

    $scope.selectPrefix = function (onCompletion) {
        var indexUrl = $scope.formUrl("index", "index.json");
        $http.get(indexUrl).then(function (response) {
            $scope.selectedPrefix = response.data.prefix;
            $scope.prefixSelections = response.data.prefix;
            $scope.status.selectPrefix = true;
            if (onCompletion != null) {
                onCompletion();
            };
        });
    }

    $scope.checkPrefix = function () {
        for (i = 0; i < $scope.selectedPrefix.length; i++) {
            if ($scope.selectedPrefix[i][0] == $scope.current.prefix) {
                $scope.current.mode = $scope.selectedPrefix[i][2];
                $scope.current.editable = ($scope.current.mode == 1);
            }
        }
        $scope.current.prefixSet = true;
    }

    $scope.doneSelectPrefixIFrame = function () {
        $scope.checkPrefix();
    }

    $scope.doneSelectPrefix = function () {
        $scope.checkPrefix();
        $scope.downloadImageMetadata();
        // $scope.status.selectPrefix = false;
    }

    $scope.formImageUrl = function (file) {
        var metadataUrl = [$scope.current.cdn, $scope.current.prefix, file].join('/')
        return metadataUrl;
    }

    $scope.formUrl = function (prefix, file) {
        var metadataUrl = [$scope.current.cdn, prefix, file].join('/')
        return metadataUrl;
    }


    $scope.form_image_name = function (file, tag) {
        var prefix = file.split(".")[0];
        if (tag == "overlay") {
            return "overlay_" + file;
        } else if (tag == "seg") {
            return "seg_" + prefix + ".png";
        } else {
            return file;
        }
    }

    $scope.downloadImageMetadata = function (onCompletion) {
        if ($scope.current.prefix == null || 0 == $scope.current.prefix.length) {
            return;
        }
        var metadataUrl = $scope.formImageUrl("metadata.json");
        $http.get(metadataUrl).then(function (response) {
            $scope.metadata = response.data;
            $log.log($scope.metadata);
            if (onCompletion != null) {
                onCompletion();
            } else {
                $scope.showImage();
            };
        });
    }

    $scope.showImage = function () {
        var selectrow = $scope.selection.row;
        if (selectrow == null) { selectrow = 0 };
        if (selectrow >= $scope.metadata.images.length) {
            selectrow = $scope.metadata.images.length - 1;
        }
        var imagerow = $scope.metadata.images[selectrow];
        var selectcol = $scope.selection.col;
        if (selectcol == null) { selectcol = 0 };
        if (selectcol >= imagerow.length) {
            selectcol = imagerow.length - 1;
        }
        var oneimage = imagerow[selectcol];
        var imgname = oneimage["image"];
        var onemeta = {};
        onemeta["latitude"] = oneimage["lat"];
        onemeta["longitude"] = oneimage["lon"];
        $scope.currentImageName = imgname;
        var imageSelected = $scope.form_image_name(imgname, $scope.selection.tag);
        var imageUrl = $scope.formImageUrl(imageSelected);
        var tm = "";
        if ($scope.editable) {
            tm = '?' + new Date().getTime();
        }
        $scope.currentImage = imageUrl + tm;
        $scope.currentOrg = $scope.formImageUrl($scope.form_image_name(imgname, "image")) + tm;
        $scope.currentSeg = $scope.formImageUrl($scope.form_image_name(imgname, "seg")) + tm;
        $scope.onemeta = JSON.stringify(onemeta);
    }

    $scope.canUp = function () {
        return $scope.selection.row > 0;
    }

    $scope.moveUp = function () {
        if ($scope.canUp()) {
            $scope.selection.row -= 1;
        }
        $scope.showImage();
    }

    $scope.canDown = function () {
        return $scope.selection.row < $scope.metadata.images.length - 1;
    }

    $scope.moveDown = function () {
        if ($scope.canDown()) {
            $scope.selection.row += 1;
        }
        $scope.showImage();
    }

    $scope.canLeft = function () {
        return $scope.selection.col > 0;
    }

    $scope.moveLeft = function () {
        if ($scope.canLeft()) {
            $scope.selection.col -= 1;
        }
        $scope.showImage();
    }

    $scope.canRight = function () {
        return $scope.selection.col < $scope.metadata.images[$scope.selection.row].length - 1;
    }

    $scope.moveRight = function () {
        if ($scope.canRight()) {
            $scope.selection.col += 1;
        }
        $scope.showImage();
    }


    $scope.changeCurrent = function () {
        $log.log($scope.current);
        var post = $scope.current;
        var postInfo = $http.post('/api/Image/SetCurrent', post);

        var successFunc = function (response) {
            $scope.onSuccess("changeCurrent", response, data => $scope.current = data);
        };

        postInfo.then(successFunc, $scope.onError);
    };


    $scope.submit = function () {
        if ($scope.form.file.$valid && $scope.file) {
            $scope.upload($scope.file);
        }
    };

    $scope.clearResult = function () {
        $scope.info = [];
        delete $scope.errorMsg;
    };

    $scope.uploadPic = function (file) {
        var postdata = {
            file: file,
            prefix: $scope.current.prefix,
            row: $scope.selection.row,
            col: $scope.selection.col,
            operation: $scope.selection.operation
        };
        file.upload = Upload.upload({
            url: 'api/Recog/UploadImage',
            data: postdata,
        });

        // upload on file select or drop
        file.upload.then(function (response) {
            $scope.imagename = response.data.result.file.output;
            $scope.info = $scope.imagename.split("\n").filter(function (el) { return el.length != 0 });
        }, function (response) {
            if (response.status > 0)
                $scope.errorMsg = response.status + ': ' + response.data;
        }, function (evt) {
            // Math.min is to fix IE which reports 200% sometimes
            file.progress = Math.min(100, parseInt(100.0 * evt.loaded / evt.total));
        });
    };

    $scope.launchMap = function (lat, lon, level, scale) {

        var map = new Microsoft.Maps.Map('#myMap', {
            credentials: "ArVGTwXaB6QlXN1XMCGlIgjnXcOGRUhHUw6_gIgU2k62v9D8YS8Imk_97jPzxW73",
            center: new Microsoft.Maps.Location(lat, lon),
            mapTypeId: Microsoft.Maps.MapTypeId.aerial,
            zoom: level,
            minZoom: level - 3,
            maxZoom: level + scale - 1,
            allowHidingLabelsOfRoad: true,
            allowInfoboxOverflow: true,
            disableKeyboardInput: true,
            showTermsLink: false
        });

        $scope.map = map;
        //Create some shapes to convert into GeoJSON.
        var polygon = new Microsoft.Maps.Polygon([
            [new Microsoft.Maps.Location(41, -104.05),
            new Microsoft.Maps.Location(45, -104.05),
            new Microsoft.Maps.Location(45, -111.05),
            new Microsoft.Maps.Location(41, -111.05),
            new Microsoft.Maps.Location(41, -104.05)]]);

        var pin = new Microsoft.Maps.Pushpin(new Microsoft.Maps.Location(43, -107.55));

        //Create an array of shapes.
        var shapes = [polygon, pin];

        //Add the shapes to the map so we can see them (optional).
        map.entities.push(shapes);

        Microsoft.Maps.loadModule('Microsoft.Maps.GeoJson', function () {
            //Convert the array of shapes into a GeoJSON object.
            // var geoJson = Microsoft.Maps.GeoJson.write(shapes);

            //Display the GeoJson in a new Window.
            // var myWindow = window.open('', '_blank', 'scrollbars=yes, resizable=yes, width=400, height=100');
            // myWindow.document.write(JSON.stringify(geoJson));
        });
    };

    

    $scope.doneSelectPrefixBrowseContour = function () {
        $scope.mapkey = "contour";
        $scope.mapext = ".json";
        $scope.doneSelectPrefixBrowseMap();
    };

    $scope.doneSelectPrefixBrowseMap = function () {
        $scope.downloadImageMetadata($scope.downloadAdditionalMetadata);
    };

    $scope.downloadAdditionalMetadata = function () {

        var onemeta = {};
        onemeta["minlat"] = $scope.metadata.minlat;
        onemeta["maxlat"] = $scope.metadata.maxlat;
        onemeta["minlon"] = $scope.metadata.minlon;
        onemeta["maxlon"] = $scope.metadata.maxlon;
        onemeta["level"] = $scope.metadata.level;
        var lat = ($scope.metadata.minlat + $scope.metadata.maxlat) / 2.0;
        var lon = ($scope.metadata.minlon + $scope.metadata.maxlon) / 2.0;
        var level = $scope.metadata.level;
        var scale = 1;
        if ("scale" in $scope.metadata) {
            scale = $scope.metadata.scale;
        };
        $scope.launchMap(lat, lon, level, scale);
        $scope.onemeta = JSON.stringify(onemeta);
        $scope.totalToLoad = 0;
        $scope.totalLoaded = 0;
        $scope.dataBounds == null;
        $scope.map.entities.clear();
        var imgarr = $scope.metadata.images;
        for (tiley = 0; tiley < imgarr.length; tiley++) {
            for (tilex = 0; tilex < imgarr[tiley].length; tilex++) {
                $scope.totalToLoad += 1;
                try {
                    var oneimage = imgarr[tiley][tilex];
                    var imgname = oneimage.image;
                    var basename = imgname.split(".")[0];
                    var geojson_url = $scope.formImageUrl($scope.mapkey + "_" + basename + $scope.mapext);
                    $http.get(geojson_url).then(function (response) {
                        $scope.totalLoaded += 1;
                        var onegeojson = response.data;

                        var shapes = [];
                        // Interpret geojson
                        // var polygons = onegeojson.


                        // var geoJsonText = JSON.stringify(onegeojson);
                        var shapes = Microsoft.Maps.GeoJson.read(onegeojson, {
                            polygonOptions: {
                                fillColor: 'rgba(0,0,30,0.0)',
                                strokeColor: 'blue',
                                strokeThickness: 1
                            }
                        });
                        $scope.map.entities.push(shapes);
                        var geoJson = Microsoft.Maps.GeoJson.write(shapes);

                        var bounds = Microsoft.Maps.LocationRect.fromShapes(shapes);
                        if ($scope.dataBounds) {
                            $scope.dataBounds = Microsoft.Maps.LocationRect.merge($scope.dataBounds, bounds);
                        } else {
                            $scope.dataBounds = bounds;
                        }
                        onemeta["bounds"] = $scope.dataBounds;
                        // if ($scope.totalToLoad > $scope.totalLoaded - 5) {
                        //    $scope.map.setView({ bounds: $scope.dataBounds, padding: 50 });
                        // };
                    });

                } catch (err) {

                };
            };
        };
    };

    $scope.getCurrent($scope.downloadImageMetadata);


    //seg数据
    var segImageData = new Array();
    //设置不同区块线条颜色
    var block_line_map = {};
    //遍历颜色,获得对应的区块表
    var blockMap = new Array();
    //设置canvas状态队列，用于undo和redo
    var canvasStatus = new Array();
    //设置当前操作队列位置
    var nowposition;
    //两张图片都加载完成标志
    var onloadFlag = false;
    //设置当前状态：STATUS_NATURE(自然状态，用户未做什么选择)/STATUS_MERGE(地块融合状态)/STATUS_SLICE(地块切割状态)/STATUS_SLICING(正在进行地块切割状态)
    var nowStatus;
    const STATUS_NATURE = 0;
    const STATUS_MERGE = 1;
    const STATUS_SLICE = 2;
    const STATUS_SLICING = 3;
    //设置区块融合的数组
    var mergeArray = new Array();
    //设置地块切割时的临时数据
    var slicingdata;
    //设置切割的区域
    var slicingblock;
    //设置图像提取精度排除干扰
    var precision = 18;
    //设置按钮绑定
    var isbanding = false;

    //canvas
    var c;
    var cxt;
    var canvaswidth;
    var canvasheight;
    //加载seg
    var img_seg_b = new Image();
    //加载img
    var img = new Image();
    $scope.startEditImage = function () {

        $scope.current.editMode = true;
        //如果没有按钮绑定，则再进行绑定一次
        if (!isbanding) {
            Binding();
            isbanding = true;
        }

        clearAll();
        img_seg_b.crossOrigin = "Anonymous";
        // img_seg_b.src = "https://skypulischinanorth.blob.core.chinacloudapi.cn/public/demo/wangcheng/region1/seg_wangcheng_1000_21000.png";
        img_seg_b.src = $scope.currentSeg;
        img_seg_b.onload = function () {
            //off screen绘制seg数据
            var ofcn = document.createElement('canvas');
            var ofc = ofcn.getContext('2d');
            ofcn.width = canvaswidth;
            ofcn.height = canvasheight;
            ofc.drawImage(img_seg_b, 0, 0, canvaswidth, canvasheight);
            //获得seg数据
            var imageData = ofc.getImageData(0, 0, canvaswidth, canvasheight);

            //保留初始seg数据
            segImageData.push(getCopyImageData(imageData));

            //处理seg图像
            segContours(imageData);

            //保留初始Status
            var initialData = getCopyImageData(imageData);

            canvasStatus.push(initialData);

            //初始化画布,此时seg图片已经加载完成了
            //判断两张图片是否都加载完成
            if (onloadFlag) {
                //初始状态设置操作位置为0
                nowposition = 0;
                //初始化按钮状态
                refreshButton();
                clearCanvas();
            } else {
                onloadFlag = true;
            }
        };

        img.crossOrigin = 'Anonymous';
        // img.src = "https://skypulischinanorth.blob.core.chinacloudapi.cn/public/demo/wangcheng/region1/wangcheng_1000_21000.png";
        img.src = $scope.currentOrg;
        img.onload = function () {

            //初始化画布,此时背景图片已经加载完成了
            //判断两张图片是否都加载完成
            if (onloadFlag) {
                //初始状态设置操作位置为0
                nowposition = 0;
                //初始化按钮状态
                refreshButton();
                clearCanvas();
            } else {
                onloadFlag = true;
            }
            //初始化系统设置
            //初始化用户状态
            nowStatus = STATUS_NATURE;

        }

    }
    function Binding() {

        c = document.getElementById("canvasOutput");
        cxt = c.getContext("2d");

        //设置canvas的显示高度和宽度
        canvaswidth = cxt.canvas.width;
        canvasheight = cxt.canvas.height;

        //设置canvas上的点击事件
        c.onclick = function (e) {

            //如果状态为STATUS_NATURE，则只高亮选中区域
            if (nowStatus == STATUS_NATURE) {
                refreshCanvas(canvasStatus[nowposition]);

                //获得最近保存的status
                var tempData = getCopyImageData(canvasStatus[nowposition]);

                //var myColor = ofc.getImageData(e.offsetX, e.offsetY, 1, 1);
                //当前鼠标在canvas中的坐标为(e.offsetX, e.offsetY)
                //根据坐标计算颜色数据开始位置为e.offsetX*4 + e.offsetY*4*canvaswidth;
                var loc = e.offsetX * 4 + e.offsetY * 4 * canvaswidth;

                highlightSelected(tempData, loc);

                refreshCanvas(tempData);
            } else if (nowStatus == STATUS_MERGE) {
                //如果区块融合数组中没有值，表示这是用户选中的第一块
                if (mergeArray.length == 0) {
                    refreshCanvas(canvasStatus[nowposition]);
                    //获得最近保存的status
                    var tempData = getCopyImageData(canvasStatus[nowposition]);
                    //获得用户点击的位置
                    var loc = e.offsetX * 4 + e.offsetY * 4 * canvaswidth;
                    var blocknum = highlightSelected(tempData, loc);
                    //将第一次点击的数据保存到mergeArray
                    mergeArray.push(blocknum);
                    refreshCanvas(tempData);
                } else {
                    //如果不是第一次点击，则从mergeArray中去取得第一次的区块号
                    //获得最近保存的status
                    var tempData = getCopyImageData(canvasStatus[nowposition]);
                    //获得用户点击的位置
                    var loc = e.offsetX * 4 + e.offsetY * 4 * canvaswidth;
                    var blocknum = highlightSelected(tempData, loc);

                    //判断该区域是否已经被选中了
                    if (mergeArray.indexOf(blocknum) == -1) {
                        //如果之前没有被选中，则添加进mergeArray
                        mergeArray.push(blocknum);
                    }

                    //将mergeArray中所有区域变色
                    highlightSelectedByBlocks(tempData, mergeArray);
                    refreshCanvas(tempData);

                    //判断mergeArray的数量n,如果n=2(暂定),则合并区域
                    if (mergeArray.length == 2) {

                        //延迟1s执行
                        var timer = self.setInterval(function () {

                            var ac = confirm("是否合并这两个区域？");
                            if (ac) {
                                //先判断2个区域是否相连
                                //var adjacent = isAdjacent(mergeArray);
                                //获得初始seg数据，并将seg数据中mergeArray[1]的值全部变成mergeArray[0]的值
                                var dupSegData = getCopyImageData(segImageData[nowposition]);
                                //console.log("nowposition:" + nowposition);
                                for (var i = 0; i < dupSegData.data.length; i = i + 4) {
                                    if (dupSegData.data[i] == mergeArray[1]) {
                                        dupSegData.data[i] = mergeArray[0];
                                        dupSegData.data[i + 1] = mergeArray[0];
                                        dupSegData.data[i + 2] = mergeArray[0];
                                    }
                                }

                                //如果当前的状态不是最新的状态，例如是经过redo的中间态，保存后删除当前状态之后的状态
                                if (nowposition != canvasStatus.length) {
                                    canvasStatus.splice(nowposition + 1, canvasStatus.length - 1 - nowposition);
                                    segImageData.splice(nowposition + 1, segImageData.length - 1 - nowposition);
                                }

                                //当前状态+1
                                nowposition++;
                                //更新segImageData
                                segImageData.push(getCopyImageData(dupSegData));

                                //处理新的seg图像
                                segContours(dupSegData);

                                //保存新状态
                                var updatedData = getCopyImageData(dupSegData);

                                //将mergeArray中的值都改为mergeArray[0]
                                //删除数组的全部2个元素
                                mergeArray.pop();
                                mergeArray.pop();
                                //移除active class
                                $("#mergeId").removeClass("btn-success");
                                $("#mergeId").addClass("btn-default");

                                //保存当前状态
                                canvasStatus.push(updatedData);

                                //重绘并高亮选中的区域
                                highlightSelected(dupSegData, loc);
                                refreshCanvas(dupSegData);
                                refreshButton();

                                //将系统状态重置为STATUS_NATURE
                                nowStatus = STATUS_NATURE;

                                clearInterval(timer);
                                timer = null;
                            }
                            else {

                                //清空选区
                                mergeArray = [];
                                refreshCanvas(canvasStatus[nowposition]);
                                clearInterval(timer);
                                timer = null;
                            }


                        }, 300)
                    }


                }
            } else if (nowStatus == STATUS_SLICE) {
                //如果当前状态为分割状态
                refreshCanvas(canvasStatus[nowposition]);

                //获得最近保存的status
                var tempData = getCopyImageData(canvasStatus[nowposition]);

                //当前鼠标在canvas中的坐标为(e.offsetX, e.offsetY)
                //根据坐标计算颜色数据开始位置为e.offsetX*4 + e.offsetY*4*canvaswidth;
                var loc = e.offsetX * 4 + e.offsetY * 4 * canvaswidth;

                var nowblock = highlightReverseSelected(tempData, loc);

                //将当前数据保存到slicingdata
                slicingdata = tempData;
                slicingblock = nowblock;

                refreshCanvas(tempData);

                //将当前状态转换为STATUS_SLICING
                nowStatus = STATUS_SLICING;

            } else if (nowStatus == STATUS_SLICING) {
                //如果当前状态为正在切割
            }


        }


        //设置canvas上的拖动事件(用于地块分割划线)
        c.onmousedown = function (e) {
            if (nowStatus == STATUS_SLICING) {
                //off screen绘制seg数据
                var ofcn = document.createElement('canvas');
                var ofc = ofcn.getContext('2d');
                ofcn.width = canvaswidth;
                ofcn.height = canvasheight;
                ofc.strokeStyle = "rgba(0,255,0,1)";
                ofc.strokeWidth = 1;
                ofc.lineWidth = 1;

                ofc.putImageData(slicingdata, 0, 0);
                var e = e || window.event;
                cxt.moveTo(e.offsetX, e.offsetY);
                document.onmousemove = function (e) {
                    var e = e || window.event;
                    //cxt.lineTo(e.offsetX, e.offsetY);
                    //获得当前位置的像素
                    ofc.lineTo(e.offsetX, e.offsetY);
                    ofc.stroke();

                    slicingdata = ofc.getImageData(0, 0, blockMap.length, blockMap[0].length);

                    //ofc.putImageData(slicingimage, 0, 0);
                    //画背景
                    cxt.drawImage(img, 0, 0);
                    cxt.drawImage(ofc.canvas, 0, 0);
                };
            }
            document.onmouseup = function () {
                if (nowStatus == STATUS_SLICING) {
                    document.onmousemove = null;
                    document.onmouseup = null;
                    //状态切回STATUS_SLICE

                    //获得画完后的图像数据slicingdata
                    //设置maskBlockMap
                    var maskBlockMap = new Array();

                    for (var i = 0; i < blockMap.length; i++) {
                        maskBlockMap[i] = new Array(i);
                        for (var j = 0; j < blockMap[i].length; j++) {

                            //当前节点始位置：i * 4 + j * 4 * blockMap.length
                            var current_pos = i * 4 + j * 4 * blockMap.length;
                            //判断区域是否为切割区域
                            if (segImageData[nowposition].data[current_pos] == slicingblock && segImageData[nowposition].data[current_pos + 1] == slicingblock && segImageData[nowposition].data[current_pos + 2] == slicingblock) {
                                //这里的数据都是切割区域的内部数据
                                //根据透明度来判断和线条来判断
                                //获得当前区域的线条颜色
                                var linecolor = block_line_map[slicingblock];
                                if (slicingdata.data[current_pos + 3] != 0 && slicingdata.data[current_pos] != linecolor[0] && slicingdata.data[current_pos + 1] != linecolor[1] && slicingdata.data[current_pos + 2] != linecolor[2]) {
                                    //block_line_map

                                    //当前区域是切割线，则置maskBlockMap为0
                                    maskBlockMap[i][j] = 0;
                                } else {
                                    //当前区域不是切割线，则置maskBlockMap为1
                                    maskBlockMap[i][j] = 1;
                                }
                            } else {
                                maskBlockMap[i][j] = 0;
                            }
                        }
                    }

                    var numberBlock = 2;
                    var nmuberpixel = 0;
                    var numberBlockPixel = new Array();

                    function tco(f) {
                        var value;
                        var active = false;
                        var accumulated = [];

                        return function accumulator() {
                            accumulated.push(arguments);
                            if (!active) {
                                active = true;
                                while (accumulated.length) {
                                    value = f.apply(this, accumulated.shift());
                                }
                                active = false;
                                return value;
                            }
                        };
                    }

                    var sliceMaskBlock = tco(function (x, y) {
                        if (maskBlockMap[x][y] != 0) {
                            if (maskBlockMap[x][y] == 1) {
                                //console.log("nmuberpixel=" + nmuberpixel);
                                if (nmuberpixel != 0) {
                                    numberBlockPixel.push(nmuberpixel);
                                }
                                maskBlockMap[x][y] = numberBlock;
                                numberBlock++;
                                nmuberpixel = 0;
                            }

                            //向上遍历
                            if (y != 0) {
                                if (maskBlockMap[x][y - 1] == 1) {
                                    maskBlockMap[x][y - 1] = maskBlockMap[x][y];
                                    sliceMaskBlock(x, y - 1);
                                }
                            }
                            //向下遍历
                            if (y != maskBlockMap[x].length - 1) {
                                if (maskBlockMap[x][y + 1] == 1) {
                                    maskBlockMap[x][y + 1] = maskBlockMap[x][y];
                                    sliceMaskBlock(x, y + 1);
                                }
                            }
                            //向左遍历
                            if (x != 0) {
                                if (maskBlockMap[x - 1][y] == 1) {
                                    maskBlockMap[x - 1][y] = maskBlockMap[x][y];
                                    sliceMaskBlock(x - 1, y);
                                }
                            }
                            //向右遍历
                            if (x != maskBlockMap.length - 1) {
                                if (maskBlockMap[x + 1][y] == 1) {
                                    maskBlockMap[x + 1][y] = maskBlockMap[x][y];
                                    sliceMaskBlock(x + 1, y);
                                }
                            }
                            nmuberpixel++;
                        }
                    });

                    for (var i = 0; i < maskBlockMap.length; i++) {
                        for (var j = 0; j < maskBlockMap[i].length; j++) {
                            if (maskBlockMap[i][j] == 1) {
                                sliceMaskBlock(i, j);
                            }
                        }
                    }

                    //console.log("nmuberpixel=" + nmuberpixel);
                    if (nmuberpixel != 0) {
                        numberBlockPixel.push(nmuberpixel);
                    }

                    //此时，numberBlockPixel的长度应为numberBlock - 2

                    var finalNumberBlock = new Array();

                    for (var i = 0; i < numberBlockPixel.length; i++) {
                        if (numberBlockPixel[i] > precision) {
                            finalNumberBlock.push(i + 2);
                        }
                    }

                    //将

                    numberBlock = finalNumberBlock.length;
                    //console.log("numberBlock:" + numberBlock);

                    if (numberBlock == 1) {
                        alert("对不起，区域未闭合");
                        //重绘
                        //获得最近保存的status
                        var tempData = getCopyImageData(canvasStatus[nowposition]);

                        highlightReverseSelectedByBlock(tempData, slicingblock);

                        refreshCanvas(tempData);
                        slicingdata = tempData;
                    } else if (numberBlock == 2) {
                        //根据finalNumberBlock和maskBlockMap更新segImageData到下一状态
                        var dupSegData = getCopyImageData(segImageData[nowposition]);

                        //未分割后的第二块区域分配一个新的区块号
                        var anotherblock;
                        var linekeys = Object.keys(block_line_map);
                        for (var i = 0; i < 254; i++) {
                            var tempkey = 254 - i;
                            var result = linekeys.indexOf(tempkey + "");
                            if (result == -1) {
                                anotherblock = tempkey;
                                break;
                            }
                        }

                        if (anotherblock == undefined) {
                            alert("划分区域过多");
                            return;
                        }
                        //console.log("finalNumberBlock:" + finalNumberBlock);
                        for (var i = 0; i < maskBlockMap.length; i++) {
                            for (var j = 0; j < maskBlockMap[i].length; j++) {
                                //当前节点始位置：i * 4 + j * 4 * maskBlockMap.length
                                var current_pos = i * 4 + j * 4 * maskBlockMap.length;

                                //判断区域是否为切割区域
                                if (dupSegData.data[current_pos] == slicingblock && dupSegData.data[current_pos + 1] == slicingblock && dupSegData.data[current_pos + 2] == slicingblock) {
                                    //这里的数据都是切割区域的内部数据
                                    //将finalNumberBlock[0]的数据保留为原样，finalNumberBlock[1]的数据修改为新的anotherblock
                                    //其他干扰节点全部置为finalNumberBlock[0]
                                    if (maskBlockMap[i][j] == finalNumberBlock[1]) {
                                        //如果该坐标的值为新区域
                                        dupSegData.data[current_pos] = anotherblock;
                                        dupSegData.data[current_pos + 1] = anotherblock;
                                        dupSegData.data[current_pos + 2] = anotherblock;
                                        dupSegData.data[current_pos + 3] = 255;
                                    }

                                }
                            }
                        }

                        //如果当前的状态不是最新的状态，例如是经过redo的中间态，保存后删除当前状态之后的状态
                        if (nowposition != canvasStatus.length) {
                            canvasStatus.splice(nowposition + 1, canvasStatus.length - 1 - nowposition);
                            segImageData.splice(nowposition + 1, segImageData.length - 1 - nowposition);
                        }

                        //当前状态+1
                        nowposition++;
                        //更新segImageData
                        segImageData.push(getCopyImageData(dupSegData));

                        //处理新的seg图像
                        segContours(dupSegData);

                        //保存新状态
                        var updatedData = getCopyImageData(dupSegData);

                        //移除success class
                        $("#sliceId").removeClass("btn-success");
                        $("#sliceId").addClass("btn-default");

                        //保存当前状态
                        canvasStatus.push(updatedData);

                        refreshCanvas(dupSegData);
                        refreshButton();

                        //将系统状态重置为STATUS_NATURE
                        nowStatus = STATUS_NATURE;

                    } else if (numberBlock > 2) {
                        alert("对不起，区域划分过多");
                        //重绘
                        //获得最近保存的status
                        var tempData = getCopyImageData(canvasStatus[nowposition]);

                        highlightReverseSelectedByBlock(tempData, slicingblock);

                        refreshCanvas(tempData);
                        slicingdata = tempData;
                    } else {
                        alert("未知错误")
                    }
                }

            };
        }

        $("#mergeId").click(function () {
            refreshCanvas(canvasStatus[nowposition]);
            if ($(this).hasClass("btn-success")) {
                $(this).removeClass("btn-success");
                $(this).addClass("btn-default");
                //情况mergeArray
                mergeArray = [];
                //将当前用户状态改为STATUS_NATURE
                nowStatus = STATUS_NATURE;
            } else {
                $(this).removeClass("btn-default");
                $(this).addClass("btn-success");
                //将当前用户状态改为STATUS_MERGE
                nowStatus = STATUS_MERGE;
            }
        });

        $("#sliceId").click(function () {
            refreshCanvas(canvasStatus[nowposition]);
            if ($(this).hasClass("btn-success")) {
                $(this).removeClass("btn-success");
                $(this).addClass("btn-default");
                //将当前用户状态改为STATUS_NATURE
                nowStatus = STATUS_NATURE;
            } else {
                $(this).removeClass("btn-default");
                $(this).addClass("btn-success");
                //将当前用户状态改为STATUS_SLICE
                nowStatus = STATUS_SLICE;
            }
        });

        $("#undoId").click(function () {
            if (nowposition != 0) {
                nowposition--;
                refreshCanvas(canvasStatus[nowposition]);
                refreshButton();
            }
        });

        $("#redoId").click(function () {
            if (nowposition != canvasStatus.length - 1) {
                nowposition++;
                refreshCanvas(canvasStatus[nowposition]);
                refreshButton();
            }
        });
        $("#saveId").click(function () {
            submitChange();
            //保存
        });
    }

    function containsInArray(val, arr) {
        var result = arr.indexOf(val);
        if (result == -1) {
            return false;
        } else {
            return true;
        }
    }
    //处理seg图像，将边缘提取出来，内部变成透明，用于覆盖到背景图像上
    function segContours(imageData) {

        //获得图像颜色的数据表
        //imageData.data数据为一维，水平读取图像
        for (var i = 0; i < canvaswidth; i++) { //先声明一维,一维长度为canvaswidth
            blockMap[i] = new Array(i);
            for (var j = 0; j < canvasheight; j++) { //再声明二维,二维长度为canvasheight

                //根据颜色的数据表生成边缘和区块
                //首先区分边缘节点，如果一个节点是边缘节点，那么其相邻（上、下、左、右）必定有一个不同颜色的像素

                //当前节点始位置：i * 4 + j * 4 * canvaswidth
                var current_pos = i * 4 + j * 4 * canvaswidth;
                //当前节点上方位置：i * 4 + (j - 1) * 4 * canvaswidth
                var up_pos = i * 4 + (j - 1) * 4 * canvaswidth;
                //当前节点下方位置：i * 4 + (j + 1) * 4 * canvaswidth
                var down_pos = i * 4 + (j + 1) * 4 * canvaswidth;
                //当前节点左方位置：(i - 1) * 4 + j * 4 * canvaswidth
                var left_pos = (i - 1) * 4 + j * 4 * canvaswidth;
                //当前节点右方位置：(i + 1) * 4 + j * 4 * canvaswidth
                var right_pos = (i + 1) * 4 + j * 4 * canvaswidth;

                var isnew = true;
                //判断是否是区域内部节点,跟相邻节点对比
                //与上方节点进行比较

                if (j != 0) {
                    if (imageData.data[current_pos] != imageData.data[up_pos]) {
                        isnew = false;
                    }
                }

                //与下方节点进行比较
                if (j != canvasheight - 1) {
                    if (imageData.data[current_pos] != imageData.data[down_pos]) {
                        isnew = false;
                    }
                }

                //与左方节点进行比较
                if (i != 0) {
                    if (imageData.data[current_pos] != imageData.data[left_pos]) {
                        isnew = false;
                    }
                }

                //与右方节点进行比较
                if (i != canvaswidth - 1) {
                    if (imageData.data[current_pos] != imageData.data[right_pos]) {
                        isnew = false;
                    }
                }

                //更加标志判断
                if (isnew) {
                    //如果是内部节点，则将区块标志标记为0
                    blockMap[i][j] = 0;
                } else {
                    //如果是轮廓节点，则保留原标记
                    blockMap[i][j] = imageData.data[current_pos];
                    //判断该边缘是否已经设置了颜色
                    var keys = Object.keys(block_line_map);
                    var result = keys.indexOf(imageData.data[current_pos] + "");

                    if (result == -1) {

                        //如果没有设置颜色，则设置一个随机的颜色
                        //var r = Math.floor(Math.random() * 128 + 128);
                        //var g = Math.floor(Math.random() * 256);
                        //var b = Math.floor(Math.random() * 256);
                        var r = 25;
                        var g = 225;
                        var b = 255;
                        block_line_map[imageData.data[current_pos]] = [r, g, b, 180];
                    }
                }
            }
        }

        //将轮廓表映射回颜色表
        for (var i = 0; i < blockMap.length; i++) {
            for (var j = 0; j < blockMap[i].length; j++) {
                //判断是轮廓节点
                //i,j→i * 4 + j * 4 * blockMap[i].length;
                var current_pos = i * 4 + j * 4 * blockMap[i].length;
                if (blockMap[i][j] == 0) {
                    //如果不是轮廓节点，将节点透明
                    for (var k = 0; k < 4; k++) {
                        imageData.data[current_pos + 3] = 0;
                    }
                } else {
                    //如果是轮廓节点，则取出轮廓节点对应的颜色数据
                    var cc = block_line_map[blockMap[i][j]];
                    for (var k = 0; k < 4; k++) {
                        imageData.data[current_pos + k] = cc[k];
                    }
                }
            }
        }

    }

    function highlightSelected(tempData, loc) {

        //透明度
        var o = tempData.data[loc + 3];
        //获得当前区域号
        var nowblock;
        if (o == 0) {
            //如果透明度为0，那么鼠标的位置在区域内部，直接获得当前的区域
            nowblock = tempData.data[loc];
        } else {
            //如果透明度不为0，那么需要从block_line_map中获得区域与线条的映射
            //将当前线条颜色与表中的对比
            /*  var r = imageData.data[loc];
            var g = imageData.data[loc+1];
            var b = imageData.data[loc+2];
            for(var key in block_line_map){
              if(block_line_map[key][0] == r && block_line_map[key][1] == g && block_line_map[key][2] == b) {
                    nowblock = key;
                    break;
              }
            } */
            //点到线条上就没有反应
            return -1;
        }
        //console.log("block:" + nowblock);
        //将改区域号的所有颜色都改变
        //首先改变区域内部的颜色
        for (var i = 0; i < tempData.data.length; i = i + 4) {
            if (tempData.data[i] == nowblock && tempData.data[i + 1] == nowblock && tempData.data[i + 2] == nowblock && tempData.data[i + 3] != 255) {
                tempData.data[i] = 25;
                tempData.data[i + 1] = 25;
                tempData.data[i + 2] = 25;
                tempData.data[i + 3] = 175;
            }
        }
        //返回当前区块号
        return nowblock;
    }

    function highlightReverseSelected(tempData, loc) {

        //透明度
        var o = tempData.data[loc + 3];
        //获得当前区域号
        var nowblock;
        if (o == 0) {
            //如果透明度为0，那么鼠标的位置在区域内部，直接获得当前的区域
            nowblock = tempData.data[loc];
        } else {
            //如果透明度不为0，那么需要从block_line_map中获得区域与线条的映射
            //将当前线条颜色与表中的对比
            /*  var r = imageData.data[loc];
            var g = imageData.data[loc+1];
            var b = imageData.data[loc+2];
            for(var key in block_line_map){
              if(block_line_map[key][0] == r && block_line_map[key][1] == g && block_line_map[key][2] == b) {
                    nowblock = key;
                    break;
              }
            } */
            //点到线条上就没有反应
            return -1;
        }

        var tempSegData = segImageData[nowposition];
        for (var i = 0; i < tempSegData.data.length; i = i + 4) {
            if (tempSegData.data[i] != nowblock) {
                tempData.data[i] = 25;
                tempData.data[i + 1] = 25;
                tempData.data[i + 2] = 25;
                tempData.data[i + 3] = 175;
            }
        }

        //for (var i = 0; i < tempData.data.length; i = i + 4) {
        //if (tempData.data[i] != nowblock && tempData.data[i + 3] != 255) {
        //tempData.data[i] = 25;
        //tempData.data[i + 1] = 25;
        //tempData.data[i + 2] = 25;
        //tempData.data[i + 3] = 175;
        //}
        //}
        //返回当前区块号
        return nowblock;
    }

    function highlightSelectedByBlocks(tempData, nowblocks) {

        //console.log("block:" + nowblock);
        //将改区域号的所有颜色都改变
        //首先改变区域内部的颜色
        for (var i = 0; i < tempData.data.length; i = i + 4) {
            if (nowblocks.indexOf(tempData.data[i]) != -1 && nowblocks.indexOf(tempData.data[i + 1]) != -1 && nowblocks.indexOf(tempData.data[i + 2]) != -1 && tempData.data[i + 3] != 255) {
                tempData.data[i] = 25;
                tempData.data[i + 1] = 25;
                tempData.data[i + 2] = 25;
                tempData.data[i + 3] = 175;
            }
        }

    }

    function highlightReverseSelectedByBlock(tempData, nowblock) {

        var tempSegData = segImageData[nowposition];
        for (var i = 0; i < tempSegData.data.length; i = i + 4) {
            if (tempSegData.data[i] != nowblock) {
                tempData.data[i] = 25;
                tempData.data[i + 1] = 25;
                tempData.data[i + 2] = 25;
                tempData.data[i + 3] = 175;
            }
        }

    }

    function isAdjacent(mergeArray) {
        var adjacent = false;
        //如果2个区域是相连的，那么其边界必然是相互靠近的,即一块区域的边界(例如mergeArray[0]的边界上，必定存在一点，改点的上、下、左、右的某个方向是mergeArray[1]的边界)
        //遍历blockMap
        for (var i = 0; i < blockMap.length; i++) {
            for (var j = 0; j < blockMap[i].length; j++) {
                if (blockMap[i][j] == mergeArray[0]) {
                    //如果改点为mergeArray[0]的边界，判断该节点的上下左右
                    //与上方节点比较
                    if (j != 0) {
                        if (blockMap[i][j - 1] == mergeArray[1]) {
                            adjacent = true;
                            break;
                        }
                    }
                    //与下方节点比较
                    if (j != blockMap[i].length - 1) {
                        if (blockMap[i][j + 1] == mergeArray[1]) {
                            adjacent = true;
                            break;
                        }
                    }
                    //与左方节点比较
                    if (i != 0) {
                        if (blockMap[i - 1][j] == mergeArray[1]) {
                            adjacent = true;
                            break;
                        }
                    }
                    //与右方节点比较
                    if (i != blockMap.length - 1) {
                        if (blockMap[i + 1][j] == mergeArray[1]) {
                            adjacent = true;
                            break;
                        }
                    }
                }
            }
        }
        alert(adjacent);
        return adjacent;
    }

    function getCopyImageData(imageData) {
        var c = document.createElement('canvas')
        c.width = imageData.width;
        c.height = imageData.height;
        var ct = c.getContext("2d");
        var copyData = ct.createImageData(imageData.width, imageData.height);
        for (var i = 0; i < imageData.data.length; i++) {
            copyData.data[i] = imageData.data[i];
        }

        return copyData;
    }

    function refreshCanvas(status) {

        //画背景
        cxt.drawImage(img, 0, 0);

        //off screen绘制seg数据
        var ofcn = document.createElement('canvas');
        var ofc = ofcn.getContext('2d');
        ofcn.width = canvaswidth;
        ofcn.height = canvasheight;

        ofc.putImageData(status, 0, 0);
        cxt.drawImage(ofc.canvas, 0, 0);
    }

    function sortAndMerge(a, b) {
        if (a < b) {
            return a + "" + b;
        } else {
            return b + "" + a;
        }
    }

    function refreshButton() {
        if (nowposition == 0) {
            $('#saveId').attr("disabled", true);
            $('#undoId').attr("disabled", true);
            if (canvasStatus.length == 1) {
                $('#redoId').attr("disabled", true);
            } else {
                $('#redoId').attr("disabled", false);
            }
        } else if (nowposition == canvasStatus.length - 1) {
            $('#saveId').attr("disabled", false);
            $('#undoId').attr("disabled", false);
            $('#redoId').attr("disabled", true);
        } else {
            $('#saveId').attr("disabled", false);
            $('#undoId').attr("disabled", false);
            $('#redoId').attr("disabled", false);
        }
    }

    function clearCanvas() {
        //off screen绘制
        refreshCanvas(canvasStatus[0]);
    }

    function clearAll() {

        cxt.clearRect(0, 0, c.width, c.height);
        segImageData = new Array();
        block_line_map = {};
        blockMap = new Array();
        canvasStatus = new Array();
        nowposition = 0;
        onloadFlag = false;
        mergeArray = new Array();
    }

    function submitChange() {
        var overlayUrl = c.toDataURL("image/png");
        var ofcn = document.createElement("canvas");
        var ofc = ofcn.getContext('2d');
        ofcn.width = cxt.canvas.width;
        ofcn.height = cxt.canvas.height;
        ofc.putImageData(segImageData[nowposition], 0, 0);
        var segUrl = ofcn.toDataURL("image/png");
        $log.log(overlayUrl);
        $log.log(segUrl);

        $scope.uploadSegAndOverlay(overlayUrl, segUrl, clearAll);
    }


    $scope.uploadSegAndOverlayOld = function (overlayUrl, segUrl) {
        var uploadAction = Upload.upload({
            url: '/api/Image/UploadImage',
            data: {
                prefix: $scope.current.prefix,
                row: $scope.selection.row,
                col: $scope.selection.col,
                // files: [overlayUrl, segUrl]
            }
        }); // .base64DataUrl([overlayUrl, segUrl]);

        var successFunc = function (resp) {
            $log.log("Upload success ... ");
        };

        var errorFunc = function (resp) {
            $log.log('Error upload, status:  ' + resp.status);
        };

        var progFunc = function (evt) {
            var progressPercentage = parseInt(100.0 * evt.loaded / evt.total);
            $log.log('progress: ' + progressPercentage + '% ');
        };

        uploadAction.base64DataUrl([overlayUrl, segUrl]).then(successFunc, errorFunc, progFunc);

    }

    $scope.uploadSegAndOverlay = function (overlayUrl, segUrl, onCompletion) {
        var overlayData = overlayUrl.substr(overlayUrl.indexOf('base64,') + 'base64,'.length);
        var segData = segUrl.substr(segUrl.indexOf('base64,') + 'base64,'.length);
        // var segData = segUrl;
	    var postUpload = $http.post('/api/Image/UploadSegmentation', {
            prefix: $scope.current.prefix,
            row: $scope.selection.row,
            col: $scope.selection.col,
            name: $scope.currentImageName,
            overlay: overlayData,
            seg: segData
        });

        var successFunc = function (response) {
            $log.log("Upload success ... ");
            if (onCompletion != null) {
                onCompletion();
            } else {
                $scope.editMode = false;
            }
        };

        postUpload.then(successFunc, $scope.onError);
    }

}]);
