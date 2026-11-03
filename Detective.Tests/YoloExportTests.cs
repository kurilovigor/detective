using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Detective.Database;
using Detective.Database.Entities;
using Detective.ViewModels;
using Detective.Views.Pages;
using Detective.Yolo.Export;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Detective.Tests
{
    [TestClass]
    public class YoloExportTests
    {
        private string _testDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "DetectiveYoloTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private string CreateTestImage(string name, int width, int height)
        {
            var imagePath = Path.Combine(_testDir, name);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Blue, null, new Rect(0, 0, width, height));
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var stream = new FileStream(imagePath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }

            return imagePath;
        }

        [TestMethod]
        public void BuildDataYaml_GeneratesExpectedYamlStructure()
        {
            var labels = new[] { "head", "helmet", "person" };
            var yaml = YoloExporter.BuildDataYaml(labels, "data");

            StringAssert.Contains(yaml, "path: data");
            StringAssert.Contains(yaml, "train: images/train");
            StringAssert.Contains(yaml, "val: images/val");
            StringAssert.Contains(yaml, "nc: 3");
            StringAssert.Contains(yaml, "names: ['head', 'helmet', 'person']");
        }

        [TestMethod]
        public void Export_CreatesDataYamlAndProjectDirectoriesAndAnnotationFileInLabelsTrain()
        {
            var imagePath = CreateTestImage("image1.png", 1000, 500);

            var dbPath = Path.Combine(_testDir, "test.detective");
            using (var db = new DetectionDatabase(dbPath))
            {
                db.Labels.Insert(new LabelEntity { Name = "head" });
                db.Labels.Insert(new LabelEntity { Name = "helmet" });
                db.Labels.Insert(new LabelEntity { Name = "person" });

                var fileEntity = new FileEntity
                {
                    Path = imagePath,
                    Detections = new System.Collections.Generic.List<DetectionEntity>
                    {
                        new DetectionEntity
                        {
                            Id = Guid.NewGuid(),
                            X = 560,
                            Y = 100,
                            Width = 114,
                            Height = 150,
                            Label = "helmet"
                        }
                    }
                };
                db.Files.Insert(fileEntity);
                db.Commit();

                var exportPath = Path.Combine(_testDir, "export", "data.yml");
                var exporter = new YoloExporter();
                exporter.Export(db, exportPath);

                // 1. Verify data.yml
                Assert.IsTrue(File.Exists(exportPath));
                var yamlContent = File.ReadAllText(exportPath);
                StringAssert.Contains(yamlContent, "path: data");
                StringAssert.Contains(yamlContent, "train: images/train");
                StringAssert.Contains(yamlContent, "val: images/val");
                StringAssert.Contains(yamlContent, "nc: 3");
                StringAssert.Contains(yamlContent, "names: ['head', 'helmet', 'person']");

                // 2. Verify folder structure:
                // data
                // data/images/train
                // data/images/val
                // data/labels/train
                // data/labels/val
                var projectDir = Path.Combine(_testDir, "export", "data");
                Assert.IsTrue(Directory.Exists(projectDir), "Project folder 'data' should exist");
                Assert.IsTrue(Directory.Exists(Path.Combine(projectDir, "images", "train")), "images/train should exist");
                Assert.IsTrue(Directory.Exists(Path.Combine(projectDir, "images", "val")), "images/val should exist");
                Assert.IsTrue(Directory.Exists(Path.Combine(projectDir, "labels", "train")), "labels/train should exist");
                Assert.IsTrue(Directory.Exists(Path.Combine(projectDir, "labels", "val")), "labels/val should exist");

                // 3. Verify annotation file in data/labels/train/image1.txt
                var annotationTxtPath = Path.Combine(projectDir, "labels", "train", "image1.txt");
                Assert.IsTrue(File.Exists(annotationTxtPath), "Annotation txt should exist in data/labels/train");

                // Verify annotation file is NOT created in source image dir or export root
                Assert.IsFalse(File.Exists(Path.Combine(_testDir, "image1.txt")), "Annotation txt should NOT exist next to image");
                Assert.IsFalse(File.Exists(Path.Combine(_testDir, "export", "image1.txt")), "Annotation txt should NOT exist in export root");

                var lines = File.ReadAllLines(annotationTxtPath);
                Assert.HasCount(1, lines);

                // Class 1 is helmet.
                // center x = (560 + 57) / 1000 = 0.617
                // center y = (100 + 75) / 500 = 0.35
                // width = 114 / 1000 = 0.114
                // height = 150 / 500 = 0.3
                var parts = lines[0].Split(' ');
                Assert.AreEqual("1", parts[0]);
                Assert.AreEqual("0.617", parts[1]);
                Assert.AreEqual("0.35", parts[2]);
                Assert.AreEqual("0.114", parts[3]);
                Assert.AreEqual("0.3", parts[4]);
            }
        }

        [TestMethod]
        public void MainWindowViewModel_ExportToYolo_ExportsSuccessfully()
        {
            var imagePath = CreateTestImage("sample.png", 200, 100);
            var dbPath = Path.Combine(_testDir, "test_vm.detective");

            using (var db = new DetectionDatabase(dbPath))
            {
                db.Labels.Insert(new LabelEntity { Name = "cat" });
                db.Labels.Insert(new LabelEntity { Name = "dog" });

                var fileEntity = new FileEntity
                {
                    Path = imagePath,
                    Detections = new System.Collections.Generic.List<DetectionEntity>
                    {
                        new DetectionEntity
                        {
                            Id = Guid.NewGuid(),
                            X = 20,
                            Y = 10,
                            Width = 40,
                            Height = 30,
                            Label = "dog"
                        }
                    }
                };
                db.Files.Insert(fileEntity);
                db.Commit();

                var vm = new MainWindowViewModel
                {
                    CurrentDatabase = db
                };

                var exportPath = Path.Combine(_testDir, "vm_export", "data.yml");
                vm.ExportToYolo(exportPath);

                Assert.IsTrue(File.Exists(exportPath));
                var exportTxt = Path.Combine(_testDir, "vm_export", "data", "labels", "train", "sample.txt");
                Assert.IsTrue(File.Exists(exportTxt));

                var lines = File.ReadAllLines(exportTxt);
                Assert.HasCount(1, lines);
                // dog is index 1
                StringAssert.StartsWith(lines[0], "1 ");
            }
        }

        [TestMethod]
        public void Export_IgnoresFilesWithoutDetections()
        {
            var imagePath = CreateTestImage("no_detections.png", 100, 100);
            var dbPath = Path.Combine(_testDir, "empty_test.detective");

            using (var db = new DetectionDatabase(dbPath))
            {
                db.Labels.Insert(new LabelEntity { Name = "car" });
                db.Files.Insert(new FileEntity
                {
                    Path = imagePath,
                    Detections = new System.Collections.Generic.List<DetectionEntity>()
                });
                db.Commit();

                var exportPath = Path.Combine(_testDir, "export_empty", "data.yml");
                var exporter = new YoloExporter();
                exporter.Export(db, exportPath);

                Assert.IsTrue(File.Exists(exportPath));
                var exportTxt = Path.Combine(_testDir, "export_empty", "data", "labels", "train", "no_detections.txt");
                Assert.IsFalse(File.Exists(exportTxt), "Should not create txt file for image without annotations");
            }
        }

        [STATestMethod]
        public void MainWindowViewModel_ExportToYolo_WithCurrentPage_SavesUnsavedDetections()
        {
            var imagePath = CreateTestImage("active_image.png", 400, 200);
            var dbPath = Path.Combine(_testDir, "page_test.detective");

            using (var db = new DetectionDatabase(dbPath))
            {
                db.Labels.Insert(new LabelEntity { Name = "box" });

                var fileEntity = new FileEntity
                {
                    Path = imagePath,
                    Detections = new System.Collections.Generic.List<DetectionEntity>()
                };
                db.Files.Insert(fileEntity);
                db.Commit();

                var page = new LabelEditPage(db);
                var fileItem = new FileItem(fileEntity.Id, "active_image.png", imagePath, null);
                page.CurrentFileItem = fileItem;

                // User drew a rectangle in the editor that hasn't been saved yet
                page.Editor.Rectangles.Add(new LabelRect(Guid.NewGuid(), new Rect(100, 50, 200, 100), "box"));

                var vm = new MainWindowViewModel
                {
                    CurrentPage = page
                };

                var exportPath = Path.Combine(_testDir, "page_export", "data.yml");
                vm.ExportToYolo(exportPath);

                Assert.IsTrue(File.Exists(exportPath));
                var exportTxt = Path.Combine(_testDir, "page_export", "data", "labels", "train", "active_image.txt");
                Assert.IsTrue(File.Exists(exportTxt));

                var lines = File.ReadAllLines(exportTxt);
                Assert.HasCount(1, lines);
                // center x = (100 + 100) / 400 = 0.5
                // center y = (50 + 50) / 200 = 0.5
                // width = 200 / 400 = 0.5
                // height = 100 / 200 = 0.5
                Assert.AreEqual("0 0.5 0.5 0.5 0.5", lines[0]);
            }
        }
    }
}

