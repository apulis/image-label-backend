var app = angular.module("fileUpload", ["xeditable", "ui.select", "ngMaterial", 'ngMessages', "ngFileUpload"]);

app.run(function (editableOptions) {
    editableOptions.theme = 'bs3';
});

app.controller('MyCtrl', ["$scope", "$filter", "$http", "$log", "$timeout", "Upload", function ($scope, $filter, $http, $log, $timeout, Upload) {
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
        file.upload = Upload.upload({
            url: '../Recog/Recog/RecogImage',
            data: { file: file },
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

}]);