describe :dir_pwd, :shared => true do
  it "returns the current working directory" do
    # On ubuntu gutsy, for example, /bin/pwd does not
    # understand -P. With just `pwd -P`, /bin/pwd is run.
    
    # The following uses inode rather than file names to account for
    # case insensitive file systems like default OS/X file systems
    platform_is_not :windows do
    File.stat(Dir.send(@method)).ino.should == File.stat(`/bin/sh -c "pwd -P"`.chomp).ino
    end
    platform_is :windows do
      File.stat(Dir.send(@method)).ino.should == File.stat(File.expand_path(`cd`.chomp)).ino
    end
  end

  platform_is :windows do
    it "can return short and long file names, with mixed case" do
      name = tmp("a path/with spaces")
      short_name = tmp("a pAtH/WiThSp~1")
      FileUtils.mkdir_p(name) if not File.directory?(name)
      Dir.chdir(short_name) do
        Dir.send(@method).should == short_name
      end
    end
  end
end
